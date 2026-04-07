using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchNameCurseSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly Content.Shared.StatusEffectNew.StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchNameCurseStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<StatusEffectContainerComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnApplied(Entity<WitchNameCurseStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (ent.Comp.OverrideName != null)
            return;

        var names = new List<string>();

        foreach (var entity in _lookup.GetEntitiesInRange(args.Target, ent.Comp.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (entity == args.Target || !HasComp<MobStateComponent>(entity))
                continue;

            names.Add(MetaData(entity).EntityName);
        }

        ent.Comp.OverrideName = names.Count > 0
            ? _random.Pick(names)
            : Loc.GetString("witch-name-curse-fallback-name");

        Dirty(ent);
    }

    private void OnTransformSpeakerName(Entity<StatusEffectContainerComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (!_status.TryEffectsWithComp<WitchNameCurseStatusEffectComponent>(ent.Owner, out var effects))
            return;

        foreach (var effect in effects)
        {
            if (string.IsNullOrWhiteSpace(effect.Comp1.OverrideName))
                continue;

            args.VoiceName = effect.Comp1.OverrideName;
            return;
        }
    }
}
