using Content.Shared.Actions;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Pirate.VulpkaninJump;

public sealed class VulpkaninJumpSystem : EntitySystem
{
    [Dependency] private readonly ThrownItemSystem _thrownItem = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VulpkaninJumpComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VulpkaninJumpComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VulpkaninJumpComponent, VulpkaninJumpActionEvent>(OnJump);
        SubscribeLocalEvent<VulpkaninJumpComponent, ThrowAttemptEvent>(OnThrowHit);
    }

    private void OnStartup(EntityUid uid, VulpkaninJumpComponent comp, ComponentStartup args) =>
        _actions.AddAction(uid, ref comp.JumpActionEntity, comp.JumpAction);

    private void OnShutdown(EntityUid uid, VulpkaninJumpComponent comp, ComponentShutdown args) =>
        _actions.RemoveAction(uid, comp.JumpActionEntity);

    private void OnJump(EntityUid uid, VulpkaninJumpComponent comp, VulpkaninJumpActionEvent args)
    {
        if (args.Handled || _container.IsEntityInContainer(uid) || _gravity.IsWeightless(uid))
            return;

        if (!_stamina.TryTakeStamina(uid, comp.StaminaCost, visual: true))
            return;

        _throwing.TryThrow(uid, args.Target, comp.JumpSpeed, uid, 10f);
        _audio.PlayPvs(comp.JumpSound, uid, comp.JumpSound?.Params);
        args.Handled = true;
    }

    private void OnThrowHit(EntityUid uid, VulpkaninJumpComponent comp, ref ThrowAttemptEvent args)
    {
        if (args.Cancelled || uid == EntityUid.Invalid || !EntityManager.EntityExists(uid) ||
            !TryComp<ThrownItemComponent>(args.ItemUid, out var thrown) ||
            args.TargetUid is not { } target || target == EntityUid.Invalid || !EntityManager.EntityExists(target) ||
            !TryComp<TransformComponent>(target, out var targetTransform))
            return;

        _thrownItem.StopThrow(uid, thrown);

        if (targetTransform.Anchored)
        {
            _stun.TryUpdateParalyzeDuration(uid, comp.WallParalyzeTime);
            _stun.TryKnockdown(uid, comp.WallKnockdownTime, true);
            args.Cancel();
            return;
        }

        if (HasComp<HumanoidAppearanceComponent>(target))
        {
            _stun.TryKnockdown(target, comp.StunTime, true);
            DropHeldItems(target);
            _stun.TryKnockdown(uid, comp.CollisionKnockdownTime, true);
        }

        args.Cancel();
    }

    private void DropHeldItems(EntityUid uid)
    {
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        foreach (var hand in hands.Hands.Keys)
            _hands.TryDrop((uid, hands), hand, checkActionBlocker: false, doDropInteraction: false);
    }
}
