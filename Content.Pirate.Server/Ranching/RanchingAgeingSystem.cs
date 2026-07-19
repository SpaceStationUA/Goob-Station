// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Ranching;
using Content.Server.Polymorph.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Ranching;

public sealed class RanchingAgeingSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> CellularDamage = "Cellular";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedSuicideSystem _suicide = default!;

    private readonly List<Entity<AnimalAgeingComponent>> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalAgeingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnimalAgeingComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _pending.Clear();
        var query = EntityQueryEnumerator<AnimalAgeingComponent>();
        while (query.MoveNext(out var uid, out var ageing))
        {
            if (HasComp<AgelessComponent>(uid) ||
                _mobState.IsDead(uid) ||
                _mobState.IsCritical(uid) ||
                _timing.CurTime < ageing.NextAgeTime)
                continue;

            ageing.NextAgeTime = _timing.CurTime +
                TimeSpan.FromSeconds(_random.NextFloat(ageing.AgeTimeMin, ageing.AgeTimeMax));
            _pending.Add((uid, ageing));
        }

        foreach (var ent in _pending)
            Age(ent);
    }

    private void OnMapInit(Entity<AnimalAgeingComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextAgeTime = _timing.CurTime +
            TimeSpan.FromSeconds(_random.NextFloat(ent.Comp.AgeTimeMin, ent.Comp.AgeTimeMax));
    }

    private void OnExamined(Entity<AnimalAgeingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"age-markup-{ent.Comp.CurrentAgeState.ToString().ToLowerInvariant()}"));
    }

    private void Age(Entity<AnimalAgeingComponent> ent)
    {
        ent.Comp.YearsOld += ent.Comp.YearsPerUpdate;

        if (ent.Comp.CurrentAgeState == AnimalAgeState.Baby &&
            ent.Comp.YearsOld >= ent.Comp.AdultHoodYear &&
            TryComp<SpawnEntityOnAgeUpComponent>(ent, out var spawn) &&
            spawn.AgeToChangeAt == AnimalAgeState.Adult &&
            spawn.EntToSpawn.Count > 0)
        {
            TransformEntity(ent.Owner, _random.Pick(spawn.EntToSpawn));
            return;
        }

        if (ent.Comp.CurrentAgeState == AnimalAgeState.Adult && ent.Comp.YearsOld >= ent.Comp.SeniorHoodYear)
            ent.Comp.CurrentAgeState = AnimalAgeState.Senior;

        if (ent.Comp.CurrentAgeState != AnimalAgeState.Senior || ent.Comp.YearsOld < ent.Comp.DeathYear)
            return;

        SpawnOldAgeResult(ent.Owner);

        if (TryComp<DamageableComponent>(ent, out var damageable))
            _suicide.ApplyLethalDamage((ent.Owner, damageable), CellularDamage);

        EnsureComp<UnrevivableComponent>(ent);
        RemComp<AnimalAgeingComponent>(ent);
    }

    private void SpawnOldAgeResult(EntityUid uid)
    {
        if (!TryComp<SpawnEntityOnOldAgeDeathComponent>(uid, out var spawn) ||
            !TryComp<HappinessComponent>(uid, out var happiness))
            return;

        if (happiness.Current <= spawn.UnHappinessRequired)
            SpawnNextToOrDrop(spawn.SadDeathEnt, uid);
        else if (happiness.Current >= spawn.HappinessRequired)
            SpawnNextToOrDrop(spawn.HappyDeathEnt, uid);
    }

    public EntityUid? TransformEntity(EntityUid uid, EntProtoId replacement)
    {
        if (TerminatingOrDeleted(uid))
            return null;

        return _polymorph.PolymorphEntity(uid, new PolymorphConfiguration
        {
            Entity = replacement,
            Forced = true,
            TransferDamage = true,
            TransferName = false,
            RevertOnCrit = false,
            RevertOnDeath = false,
            RevertOnDelete = false,
            RevertOnEat = false,
            AllowRepeatedMorphs = true,
            IgnoreAllowRepeatedMorphs = true,
            AttachToGridOrMap = true,
            ShowPopup = false,
            PolymorphPopup = null,
            ExitPolymorphPopup = null,
        });
    }
}
