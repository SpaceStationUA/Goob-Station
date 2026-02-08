using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Content.Shared.CombatMode;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.IoC;
using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Shared.Hands.EntitySystems;

namespace Content.Pirate.Client._JustDecor.Weapons.SmartRevolver;

public sealed class SmartRevolverSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private SmartRevolverOverlay _overlayInst = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlayInst = new SmartRevolverOverlay(_entity, _player, _eye);
        _overlay.AddOverlay(_overlayInst);
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnRightClick))
            .Register<SmartRevolverSystem>();
    }

    private bool OnRightClick(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return TryHandleInstantTargeting(session, coords, uid);
    }

    private bool TryHandleInstantTargeting(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        var player = session?.AttachedEntity;
        if (player == null)
            return false;

        // Instant targeting only in combat mode
        if (!_combat.IsInCombatMode(player.Value))
            return false;

        // Must be holding Smart Revolver
        if (!_hands.TryGetActiveItem(player.Value, out var activeItem))
            return false;

        if (!HasComp<Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver.SmartRevolverComponent>(activeItem))
            return false;

        EntityUid target = uid;

        if (!target.IsValid() || target == player || target == activeItem.Value)
        {
            RaiseNetworkEvent(new Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver.SmartRevolverSetTargetMessage(NetEntity.Invalid));
            return true;
        }

        if (HasComp<MobStateComponent>(target) || HasComp<DamageableComponent>(target))
        {
            RaiseNetworkEvent(new Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver.SmartRevolverSetTargetMessage(GetNetEntity(target)));
            return true;
        }

        // Клік в пусте місце -> очищення цілі
        RaiseNetworkEvent(new Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver.SmartRevolverSetTargetMessage(GetNetEntity(target)));
        return true;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay(_overlayInst);
        CommandBinds.Unregister<SmartRevolverSystem>();
    }
}
