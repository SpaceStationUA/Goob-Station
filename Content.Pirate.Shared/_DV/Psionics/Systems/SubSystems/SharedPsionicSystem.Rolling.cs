using System.Linq;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Popups;
using Content.Shared.Actions;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._DV.Psionics.Systems;

public abstract partial class SharedPsionicSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;

    public bool TryRollPsionic(Entity<PotentialPsionicComponent> potPsionic, float multiplier = 1.0f)
    {
        // Pirate: drugs only grant additional powers to entities that are already psionic.
        // Potential-only entities (e.g. roundstart losers) are unaffected by them - they can
        // only become psionic through crystals, jobs, species, traits or a random roundstart roll.
        if (!HasComp<PsionicComponent>(potPsionic))
            return false;

        if (potPsionic.Comp.Rolled)
            return false;

        potPsionic.Comp.Rolled = true;

        if (!RollChance(potPsionic, multiplier))
        {
            Popup.PopupEntity(Loc.GetString("psionic-roll-failed"), potPsionic, potPsionic, PopupType.Medium);
            return false;
        }

        AddRandomPsionicPower(potPsionic, true);
        Dirty(potPsionic);
        return true;
    }

    public bool CanRollPsionic(EntityUid uid)
    {
        // Pirate: entities without psionic potential cannot receive psionic powers.
        // Admin commands bypass this by ensuring the PotentialPsionicComponent first.
        if (!HasComp<PotentialPsionicComponent>(uid))
            return false;

        // Pirate: lets downstream insulation rules block psionic awakening without forking DV roll callers.
        var ev = new PsionicRollAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        return ev.CanRoll;
    }

    protected bool RollChance(Entity<PotentialPsionicComponent> potPsionic, float multiplier = 1.0f)
    {
        if (!CanRollPsionic(potPsionic))
            return false;

        var chance = potPsionic.Comp.BaseChance;
        // Jobs like Command and Chaplains get a bonus on their roll.
        chance += potPsionic.Comp.JobBonusChance;
        // Species like Kitsunes get a bonus on their roll.
        chance += potPsionic.Comp.SpeciesBonusChance;

        // Rolling with chemicals can have multipliers.
        chance *= multiplier;

        chance = Math.Clamp(chance, 0, 1);
        return Random.Prob(chance);
    }

    public void AddRandomPsionicPower(Entity<PotentialPsionicComponent> psionic, bool midRound)
    {
        if (!CanRollPsionic(psionic))
            return;

        if (!_prototypeManager.Resolve(psionic.Comp.PsionicPowerTableId, out var powerTable))
            return;

        var random = Random.GetRandom(); // This is called in GetSpawns(). We simply call it once to avoid calling it multiple times.
        var table = BuildRollTable(powerTable, psionic); // Pirate: merge per-psionic pool additions.
        var attempts = 0;
        while (attempts < 20) // 20 attempts to get a unique psionic power.
        {
            var spawns = _entityTable.GetSpawns(table, random);

            foreach (var entProtoId in spawns)
            {
                if (TryAddPsionicPower(psionic, midRound, entProtoId))
                    return;

                attempts++;
            }
        }

        Popup.PopupEntity(Loc.GetString("psionic-roll-failed"), psionic, psionic, PopupType.Medium);
    }

    /// <summary>
    /// Pirate: Builds the table used for a roll, merging the base power table with any
    /// per-psionic pool additions (powers unlocked by other powers, e.g. Healing Word -> Revivify).
    /// Weights are preserved relative to the base table's own weights.
    /// </summary>
    private EntityTableSelector BuildRollTable(EntityTablePrototype baseTable, Entity<PotentialPsionicComponent> psionic)
    {
        if (!TryComp<PsionicComponent>(psionic, out var psionicComp)
            || psionicComp.PowerPoolAdditions.Count == 0)
            return baseTable.Table;

        // Only flat group tables can be merged cleanly; otherwise just roll the base table.
        if (baseTable.Table is not GroupSelector group)
            return baseTable.Table;

        var children = new List<EntityTableSelector>(group.Children);
        foreach (var (protoId, weight) in psionicComp.PowerPoolAdditions)
            children.Add(new EntSelector { Id = protoId, Weight = weight });

        return new GroupSelector { Children = children };
    }

    private bool TryAddPsionicPower(Entity<PotentialPsionicComponent> psionic, bool midRound, EntProtoId entProtoId)
    {
        if (!_prototypeManager.Resolve(entProtoId, out var powerEntity))
            return false;
        // If the psionic already has that power, do not add it again.
        if (powerEntity.Components.Values
            .Where(component => component.Component is Components.PsionicPowers.BasePsionicPowerComponent)
            .Any(component => EntityManager.HasComponent(psionic, component.Component.GetType())))
            return false;
        // If they don't have it already, add it.
        EntityManager.AddComponents(psionic, powerEntity, removeExisting: false);

        // MapInitEvent may not fire for components added to already-initialized entities,
        // so manually trigger the power initialization that OnPowerInit would normally handle.
        InitializePowerComponents(psionic.Owner, powerEntity);

        if (!midRound)
            return true;
        // For alternative means of getting psionics that aren't via spawning in, cause them to suffer.
        _stuttering.DoStutter(psionic, TimeSpan.FromMinutes(1), false);
        _stun.TryKnockdown(psionic.Owner, TimeSpan.FromSeconds(3), false, drop: false);
        _jittering.DoJitter(psionic, TimeSpan.FromSeconds(3), false);

        return true;
    }

    /// <summary>
    /// Pirate: Manually triggers the power initialization that <c>OnPowerInit</c> would
    /// normally handle via <c>MapInitEvent</c>. <c>MapInitEvent</c> may not fire for
    /// components added to already-initialized entities via <c>AddComponents</c>, so
    /// callers must invoke this after adding power components from a prototype.
    /// </summary>
    public void InitializePowerComponents(EntityUid entity, EntityPrototype powerPrototype)
    {
        var actionSystem = EntityManager.System<SharedActionsSystem>();
        foreach (var (_, entry) in powerPrototype.Components)
        {
            if (entry.Component is not Components.PsionicPowers.BasePsionicPowerComponent powerComp)
                continue;

            var compType = powerComp.GetType();
            if (!EntityManager.TryGetComponent(entity, compType, out var comp))
                continue;

            var psionicPowerComp = (Components.PsionicPowers.BasePsionicPowerComponent) comp;

            // Create the action button.
            actionSystem.AddAction(entity, ref psionicPowerComp.ActionEntity, psionicPowerComp.ActionProtoId);
            Dirty(entity, psionicPowerComp);

            // Ensure the entity is registered as a psionic.
            var psionicComp = EnsureComp<PsionicComponent>(entity);
            psionicComp.PsionicPowersActionEntities.Add(psionicPowerComp.ActionEntity);
            Dirty(entity, psionicComp);

            // Merge any powers this power unlocks into the psionic's random pull.
            if (psionicPowerComp.PowerPoolAdditions.Count > 0)
            {
                foreach (var (protoId, weight) in psionicPowerComp.PowerPoolAdditions)
                    psionicComp.PowerPoolAdditions[protoId] = weight;
                Dirty(entity, psionicComp);
            }

            // Feedback shown to the player when they first gain this power.
            if (psionicPowerComp.PowerInitFeedback is { } feedback
                && TryComp<ActorComponent>(entity, out _))
            {
                RaiseLocalEvent(entity, new PsionicPowerGainedEvent(entity, Loc.GetString(feedback)), broadcast: true);
            }

            // Raise a post-init event so specialized power systems (e.g. PsionicEruptionPowerSystem)
            // receive the same initialization as MapInitEvent paths without duplicating base setup.
            RaiseLocalEvent(entity, new PsionicPowerPostInitializedEvent(compType));
        }
    }

    public bool GrantPsionicRoll(Entity<PotentialPsionicComponent?> potPsionic)
    {
        if (!Resolve(potPsionic, ref potPsionic.Comp) || !potPsionic.Comp.Rolled)
            return false;

        potPsionic.Comp.Rolled = false;
        Dirty(potPsionic);
        return true;
    }
}
