using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Content.Shared.CombatMode;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.IoC;
using Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver;
using Content.Shared.Mobs.Components;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Shared.Hands.EntitySystems;
using Content.Client.ContextMenu.UI;
using Content.Client.UserInterface.Systems.Actions;
using Content.Client.UserInterface.Systems.Hands;
using Content.Client.UserInterface.Systems.Inventory;
using Content.Client.UserInterface.Systems.Storage;

namespace Content.Pirate.Client._JustDecor.Weapons.SmartRevolver;

public sealed class SmartRevolverSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private SmartRevolverOverlay _overlayInst = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlayInst = new SmartRevolverOverlay(_entity, _player, _eye);
        _overlay.AddOverlay(_overlayInst);
        CommandBinds.Builder
            .BindAfter(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnRightClick),
                typeof(EntityMenuUIController),
                typeof(HandsUIController),
                typeof(InventoryUIController),
                typeof(StorageUIController),
                typeof(ActionUIController))
            .Register<SmartRevolverSystem>();
    }

    private bool OnRightClick(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return TryHandleInstantTargeting(session, coords, uid);
    }

    private bool TryHandleInstantTargeting(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session == null)
            return false;

        var player = session?.AttachedEntity;
        if (player == null)
            return false;

        // Instant targeting only in combat mode
        if (!_combat.IsInCombatMode(player.Value))
            return false;

        // Must be holding Smart Revolver
        if (!_hands.TryGetActiveItem(player.Value, out var activeItem))
            return false;

        if (!TryComp(activeItem.Value, out SmartRevolverComponent? revolver))
            return false;

        EntityUid target = uid;

        if (!target.IsValid() || target == player || target == activeItem.Value)
        {
            RaiseNetworkEvent(new SmartRevolverSetTargetMessage(NetEntity.Invalid));
            return true;
        }

        var playerPos = _transform.GetMapCoordinates(player.Value).Position;
        var targetPos = target.IsValid()
            ? _transform.GetMapCoordinates(target).Position
            : _transform.ToMapCoordinates(coords).Position;

        if ((targetPos - playerPos).Length() > revolver.MaxTargetDistance)
        {
            RaiseNetworkEvent(new SmartRevolverSetTargetMessage(NetEntity.Invalid));
            return true;
        }

        if (HasComp<MobStateComponent>(target) || HasComp<DamageableComponent>(target))
        {
            RaiseNetworkEvent(new SmartRevolverSetTargetMessage(GetNetEntity(target)));
            return true;
        }

        // Клік в пусте місце -> очищення цілі
        RaiseNetworkEvent(new SmartRevolverSetTargetMessage(NetEntity.Invalid));
        return true;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay(_overlayInst);
        CommandBinds.Unregister<SmartRevolverSystem>();
    }
}
