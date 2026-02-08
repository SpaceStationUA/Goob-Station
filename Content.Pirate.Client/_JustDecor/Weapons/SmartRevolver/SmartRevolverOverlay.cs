using System.Numerics;
using Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Pirate.Client._JustDecor.Weapons.SmartRevolver;

/// <summary>
/// Overlay that shows a target indicator around the selected entity for the smart revolver.
/// </summary>
public sealed class SmartRevolverOverlay : Overlay
{
    private readonly IEntityManager _entityManager;
    private readonly IPlayerManager _playerManager;
    private readonly IEyeManager _eyeManager;
    private readonly SharedTransformSystem _transform;
    private readonly SharedHandsSystem _hands;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public SmartRevolverOverlay(IEntityManager entityManager, IPlayerManager playerManager, IEyeManager eyeManager)
    {
        _entityManager = entityManager;
        _playerManager = playerManager;
        _eyeManager = eyeManager;
        _transform = _entityManager.System<SharedTransformSystem>();
        _hands = _entityManager.System<SharedHandsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalPlayer?.ControlledEntity;
        if (player == null)
            return;

        if (!_hands.TryGetActiveItem(player.Value, out var activeHandEntity))
            return;

        if (!_entityManager.TryGetComponent<SmartRevolverComponent>(activeHandEntity, out var comp))
            return;

        if (comp.SelectedTarget == null || !_entityManager.EntityExists(comp.SelectedTarget.Value))
            return;

        if (!_entityManager.TryGetComponent<TransformComponent>(comp.SelectedTarget.Value, out var targetXform))
            return;

        if (targetXform.MapID != args.MapId)
            return;

        var screenPos = _eyeManager.CoordinatesToScreen(targetXform.Coordinates);
        var handle = args.ScreenHandle;

        // Прицільний індикатор
        var color = Color.Gold;
        var boxSize = new Vector2(60, 60);
        var halfSize = boxSize / 2;
        var topLeft = screenPos.Position - halfSize;
        var borderThickness = 2.5f;

        // Зовнішня рамка
        handle.DrawRect(UIBox2.FromDimensions(topLeft, new Vector2(boxSize.X, borderThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(0, boxSize.Y - borderThickness), new Vector2(boxSize.X, borderThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft, new Vector2(borderThickness, boxSize.Y)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - borderThickness, 0), new Vector2(borderThickness, boxSize.Y)), color);

        // Кути для "lock-on" ефекту
        var cornerLength = 12f;
        var cornerThickness = 3f;

        // Верхній лівий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft - new Vector2(5, 5), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft - new Vector2(5, 5), new Vector2(cornerThickness, cornerLength)), color);

        // Верхній правий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerLength + 5, -5), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerThickness + 5, -5), new Vector2(cornerThickness, cornerLength)), color);

        // Нижній лівий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(-5, boxSize.Y - cornerThickness + 5), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(-5, boxSize.Y - cornerLength + 5), new Vector2(cornerThickness, cornerLength)), color);

        // Нижній правий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerLength + 5, boxSize.Y - cornerThickness + 5), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerThickness + 5, boxSize.Y - cornerLength + 5), new Vector2(cornerThickness, cornerLength)), color);

        // Центральна частинка
        var diamondSize = 5f;
        handle.DrawRect(UIBox2.FromDimensions(screenPos.Position - new Vector2(diamondSize / 2, diamondSize / 2), new Vector2(diamondSize, diamondSize)), color);
    }
}