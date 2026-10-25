using Content.Goobstation.Common.Grab;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Shared._White.Grab;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared._Pirate.MartialArts;

public sealed class UnathiTailSystem : EntitySystem
{
    [Dependency] private readonly SharedMartialArtsSystem _martialArts = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly GrabIntentSystem _grab = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly GrabThrownSystem _grabThrowing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrantUnathiTailComponent, ComponentInit>(OnGrantInit);
        SubscribeLocalEvent<CanPerformComboComponent, UnathiTailTripPerformedEvent>(OnTailTrip);
        SubscribeLocalEvent<CanPerformComboComponent, UnathiTailChokePerformedEvent>(OnTailChoke);
        SubscribeLocalEvent<CanPerformComboComponent, UnathiTailThrowPerformedEvent>(OnTailThrow);
    }

    private void OnGrantInit(Entity<GrantUnathiTailComponent> ent, ref ComponentInit args)
    {
        _martialArts.TryGrantMartialArt(ent.Owner, ent.Comp, confirmSwap: false);
    }

    private void OnTailTrip(Entity<CanPerformComboComponent> ent, ref UnathiTailTripPerformedEvent args)
    {
        if (!TryGetCombo(ent, out var proto, out var target, out var downed) || downed)
            return;

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, false);
        PlayCombo(ent, target, proto.Name, "/Audio/Weapons/genhit3.ogg");
    }

    private void OnTailChoke(Entity<CanPerformComboComponent> ent, ref UnathiTailChokePerformedEvent args)
    {
        if (!TryGetCombo(ent, out var proto, out var target, out _)
            || !TryComp<PullerComponent>(ent, out var puller)
            || !TryComp<GrabIntentComponent>(ent, out var grabIntent)
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<GrabbableComponent>(target, out var grabbable))
            return;

        if (!_grab.TrySetGrabStages((ent, puller, grabIntent), (target, pullable, grabbable), GrabStage.Suffocate))
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, false);
        PlayCombo(ent, target, proto.Name, "/Audio/Weapons/genhit3.ogg");
    }

    private void OnTailThrow(Entity<CanPerformComboComponent> ent, ref UnathiTailThrowPerformedEvent args)
    {
        if (!TryGetCombo(ent, out var proto, out var target, out _))
            return;

        var direction = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(ent).Position;
        direction *= 1f / direction.Length();

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, false);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _grabThrowing.Throw(target, ent, direction, proto.ThrownSpeed, behavior: false);
        PlayCombo(ent, target, proto.Name, "/Audio/Weapons/genhit2.ogg");
    }

    private bool TryGetCombo(Entity<CanPerformComboComponent> ent, out ComboPrototype proto, out EntityUid target,
        out bool downed)
    {
        proto = default!;
        target = EntityUid.Invalid;
        downed = false;

        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var combo)
            || combo.MartialArtsForm != MartialArtsForms.UnathiTailArts
            || !_martialArts.TryUseMartialArt(ent, combo, out target, out downed))
            return false;

        proto = combo;
        return true;
    }

    private void PlayCombo(Entity<CanPerformComboComponent> ent, EntityUid target, string name, string sound)
    {
        _audio.PlayPvs(new SoundPathSpecifier(sound), target);
        _martialArts.ComboPopup(ent, target, name);
        ent.Comp.LastAttacks.Clear();
    }
}
