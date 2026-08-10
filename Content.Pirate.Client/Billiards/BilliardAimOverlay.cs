// SPDX-License-Identifier: MIT

using Content.Client.Gameplay;
using Content.Client.Viewport;
using Content.Pirate.Shared.Billiards;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Pirate.Client.Billiards;

public sealed class BilliardAimOverlay : Overlay
{
    private const float AimDistance = 0.8f;
    private const float DashLength = 0.05f;
    private const float GapLength = 0.05f;

    private readonly IEntityManager _entityManager;
    private readonly IInputManager _inputManager;
    private readonly IPlayerManager _playerManager;
    private readonly IStateManager _stateManager;
    private readonly IUserInterfaceManager _uiManager;
    private readonly SharedHandsSystem _hands;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BilliardAimOverlay(
        IEntityManager entityManager,
        IPlayerManager playerManager,
        IInputManager inputManager,
        IUserInterfaceManager uiManager,
        IStateManager stateManager)
    {
        _entityManager = entityManager;
        _playerManager = playerManager;
        _inputManager = inputManager;
        _uiManager = uiManager;
        _stateManager = stateManager;

        _hands = _entityManager.System<SharedHandsSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { Valid: true } player ||
            !_entityManager.TryGetComponent<HandsComponent>(player, out var hands) ||
            !_hands.TryGetActiveItem((player, hands), out var activeItem) ||
            !_entityManager.HasComponent<BilliardCueComponent>(activeItem.Value) ||
            !TryGetHoveredBall(out var targetBall))
        {
            return;
        }

        var playerPosition = _transform.GetMapCoordinates(player);
        var ballPosition = _transform.GetMapCoordinates(targetBall);
        if (playerPosition.MapId != ballPosition.MapId || ballPosition.MapId != args.MapId)
            return;

        var offset = ballPosition.Position - playerPosition.Position;
        if (offset.LengthSquared() < 0.001f)
            return;

        var direction = offset.Normalized();
        for (var distance = 0f; distance < AimDistance; distance += DashLength + GapLength)
        {
            var start = ballPosition.Position + direction * distance;
            var end = ballPosition.Position + direction * MathF.Min(distance + DashLength, AimDistance);
            args.WorldHandle.DrawLine(start, end, Color.White);
        }
    }

    private bool TryGetHoveredBall(out EntityUid ball)
    {
        ball = default;

        if (_stateManager.CurrentState is not GameplayStateBase screen ||
            _uiManager.CurrentlyHovered is not IViewportControl viewport ||
            !_inputManager.MouseScreenPosition.IsValid)
        {
            return false;
        }

        var mousePosition = viewport.PixelToMap(_inputManager.MouseScreenPosition.Position);
        var candidates = viewport is ScalingViewport scalingViewport
            ? screen.GetClickableEntities(mousePosition, scalingViewport.Eye)
            : screen.GetClickableEntities(mousePosition);

        foreach (var candidate in candidates)
        {
            if (!_entityManager.HasComponent<BilliardBallComponent>(candidate))
                continue;

            ball = candidate;
            return true;
        }

        return false;
    }
}
