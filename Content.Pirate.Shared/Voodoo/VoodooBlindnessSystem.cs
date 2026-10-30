// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;

namespace Content.Pirate.Shared.Voodoo;

public sealed partial class VoodooBlindnessSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoodooBlindnessStatusEffectComponent, StatusEffectAppliedEvent>(OnBlindnessApplied);
        SubscribeLocalEvent<VoodooBlindnessStatusEffectComponent, StatusEffectRemovedEvent>(OnBlindnessRemoved);
        SubscribeLocalEvent<BlindableComponent, CanSeeAttemptEvent>(OnCanSeeAttempt);
    }

    private void OnBlindnessApplied(Entity<VoodooBlindnessStatusEffectComponent> ent,
        ref StatusEffectAppliedEvent args)
    {
        _blindable.UpdateIsBlind(args.Target);
    }

    private void OnBlindnessRemoved(Entity<VoodooBlindnessStatusEffectComponent> ent,
        ref StatusEffectRemovedEvent args)
    {
        _blindable.UpdateIsBlind(args.Target);
    }

    private void OnCanSeeAttempt(Entity<BlindableComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (_status.HasEffectComp<VoodooBlindnessStatusEffectComponent>(ent))
            args.Cancel();
    }
}
