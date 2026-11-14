using Content.Pirate.Common.Familiar;
using Content.Pirate.Server._DV.Psionics.Components;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using System.Linq;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

/// <summary>
///     Pirate: Summons a familiar mob bound to the psionic as its master. Which familiar is
///     summoned is fully prototype-driven via <see cref="SummonFamiliarPowerComponent.Prototype"/>
///     (e.g. the chaplain's Remilia bat, an NPC pet until a ghost takes it over). The familiar
///     can be dispelled. Only one familiar at a time - the old one must die (or be dispelled)
///     before a new one can be summoned, otherwise the cooldown is not consumed.
///     The familiar fights anyone who attacks her or her master, but never strays further than
///     <see cref="SummonFamiliarPowerComponent.LeashRange"/> from the master: past the leash it
///     drops every grudge and follows its master back.
/// </summary>
public sealed class SummonFamiliarPowerSystem : SharedSummonFamiliarPowerSystem
{
    private static readonly TimeSpan LeashCheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly CommonFamiliarSystem _familiar = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonedFamiliarComponent, MobStateChangedEvent>(OnFamiliarMobState);
        SubscribeLocalEvent<SummonedFamiliarComponent, EntityTerminatingEvent>(OnFamiliarTerminating);
        SubscribeLocalEvent<SummonFamiliarPowerComponent, DamageChangedEvent>(OnMasterDamaged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Familiars never stray far from their master: past the leash range they break off any
        // fight and follow their master back. Otherwise a fleeing or distant enemy could drag
        // them across the whole station.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SummonedFamiliarComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextLeashCheck)
                continue;

            comp.NextLeashCheck = now + LeashCheckInterval;

            var caster = comp.Caster;
            if (!Exists(caster) || TerminatingOrDeleted(caster))
                continue;

            if (!TryComp<SummonFamiliarPowerComponent>(caster, out var power))
                continue;

            if (!_transform.InRange(Transform(uid).Coordinates, Transform(caster).Coordinates, power.LeashRange))
                BreakOff(uid);
        }
    }

    protected override void OnPowerUsed(Entity<SummonFamiliarPowerComponent> psionic, ref SummonFamiliarPowerActionEvent args)
    {
        var performer = args.Performer;

        // One familiar at a time - refuse without consuming the cooldown.
        if (psionic.Comp.Summon is { } existing && Exists(existing) && !TerminatingOrDeleted(existing))
        {
            args.Handled = false;
            _popup.PopupEntity(Loc.GetString("psionic-familiar-already-summoned"), performer, performer, PopupType.SmallCaution);
            return;
        }

        psionic.Comp.Summon = SummonFamiliar(psionic, performer);

        AfterPowerUsed(psionic, performer);
    }

    private EntityUid SummonFamiliar(Entity<SummonFamiliarPowerComponent> psionic, EntityUid caster)
    {
        var familiar = Spawn(psionic.Comp.Prototype, Transform(caster).Coordinates);
        _transform.AttachToGridOrMap(familiar);

        // Track the familiar so the power can't summon a second one while this one lives,
        // and so death or dispelling clears the slot. Server-only tracking - never networked.
        var marker = EnsureComp<SummonedFamiliarComponent>(familiar);
        marker.Caster = caster;

        // Summoned familiars can be dispelled by psionics.
        EnsureComp<DispellableComponent>(familiar);

        // Anyone who attacks the familiar itself becomes her enemy. Grudges are cleared by
        // the leash (see Update) once she's pulled too far from her master to keep fighting.
        EnsureComp<NPCRetaliationComponent>(familiar);

        // Bind the familiar to its summoner.
        _familiar.SetMaster(familiar, caster);

        // Familiars follow their master like any self-respecting pet: swap the mob's own
        // HTN (bats default to SimpleHostile) for a follow compound and point it at the
        // master. Coordinates anchored to the caster track them as they move.
        if (psionic.Comp.FollowHtn is { } followHtn
            && TryComp<HTNComponent>(familiar, out var htn))
        {
            htn.RootTask = new HTNCompoundTask { Task = followHtn };
            _npc.SetBlackboard(familiar, NPCBlackboard.FollowTarget, new EntityCoordinates(caster, Vector2.Zero), htn);
            // The leash: the follow compound's combat branch only engages within this range.
            _npc.SetBlackboard(familiar, "FollowRange", psionic.Comp.LeashRange, htn);
            _htn.Replan(htn);
        }

        // Never turn on its master, even if it picks up combat branches.
        _npcFaction.IgnoreEntity(familiar, caster);

        _popup.PopupEntity(Loc.GetString("psionic-familiar-summoned"), familiar, caster);
        return familiar;
    }

    /// <summary>
    ///     The familiar avenges her master: whoever damages the psionic becomes her enemy.
    /// </summary>
    private void OnMasterDamaged(Entity<SummonFamiliarPowerComponent> psionic, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin)
            return;

        if (psionic.Comp.Summon is not { } summon
            || !Exists(summon)
            || TerminatingOrDeleted(summon)
            || !TryComp<SummonedFamiliarComponent>(summon, out var marker))
            return;

        AggroFamiliar((summon, marker), origin);
    }

    private void AggroFamiliar(Entity<SummonedFamiliarComponent> familiar, EntityUid attacker)
    {
        // No point attacking inanimate objects, and never turn on the master.
        if (!HasComp<MobStateComponent>(attacker) || attacker == familiar.Comp.Caster)
            return;

        _npcFaction.AggroEntity(familiar.Owner, attacker);
    }

    /// <summary>
    ///     Breaks off any fight: drops every forced grudge so the combat branch fails and the
    ///     follow branch brings the familiar back to its master.
    /// </summary>
    private void BreakOff(EntityUid familiar)
    {
        if (TryComp<FactionExceptionComponent>(familiar, out var exception))
        {
            foreach (var hostile in exception.Hostiles.ToArray())
                _npcFaction.DeAggroEntity(familiar, hostile);
        }

        if (TryComp<HTNComponent>(familiar, out var htn))
            _htn.Replan(htn);
    }

    private void OnFamiliarMobState(Entity<SummonedFamiliarComponent> familiar, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<SummonFamiliarPowerComponent>(familiar.Comp.Caster, out var power) || power.Summon != familiar.Owner)
            return;

        power.Summon = null;
        _popup.PopupEntity(Loc.GetString("psionic-familiar-died"), familiar, familiar.Comp.Caster, PopupType.MediumCaution);
    }

    private void OnFamiliarTerminating(Entity<SummonedFamiliarComponent> familiar, ref EntityTerminatingEvent args)
    {
        if (!TryComp<SummonFamiliarPowerComponent>(familiar.Comp.Caster, out var power) || power.Summon != familiar.Owner)
            return;

        power.Summon = null;
    }
}
