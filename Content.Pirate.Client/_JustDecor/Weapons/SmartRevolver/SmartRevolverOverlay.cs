using System.Numerics;
using Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
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
    private readonly SharedHandsSystem _hands;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public SmartRevolverOverlay(IEntityManager entityManager, IPlayerManager playerManager, IEyeManager eyeManager)
    {
        _entityManager = entityManager;
        _playerManager = playerManager;
        _eyeManager = eyeManager;
        _hands = _entityManager.System<SharedHandsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalSession?.AttachedEntity;
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
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;

        // Прицільний індикатор
        var color = Color.Gold;
        var boxSize = new Vector2(60f, 60f) * uiScale;
        var halfSize = boxSize / 2;
        var topLeft = screenPos.Position - halfSize;
        var borderThickness = 2.5f * uiScale;

        // Зовнішня рамка
        handle.DrawRect(UIBox2.FromDimensions(topLeft, new Vector2(boxSize.X, borderThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(0, boxSize.Y - borderThickness), new Vector2(boxSize.X, borderThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft, new Vector2(borderThickness, boxSize.Y)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - borderThickness, 0), new Vector2(borderThickness, boxSize.Y)), color);

        // Кути для "lock-on" ефекту
        var cornerLength = 12f * uiScale;
        var cornerThickness = 3f * uiScale;
        var cornerOffset = 5f * uiScale;

        // Верхній лівий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft - new Vector2(cornerOffset, cornerOffset), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft - new Vector2(cornerOffset, cornerOffset), new Vector2(cornerThickness, cornerLength)), color);

        // Верхній правий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerLength + cornerOffset, -cornerOffset), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerThickness + cornerOffset, -cornerOffset), new Vector2(cornerThickness, cornerLength)), color);

        // Нижній лівий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(-cornerOffset, boxSize.Y - cornerThickness + cornerOffset), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(-cornerOffset, boxSize.Y - cornerLength + cornerOffset), new Vector2(cornerThickness, cornerLength)), color);

        // Нижній правий кут
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerLength + cornerOffset, boxSize.Y - cornerThickness + cornerOffset), new Vector2(cornerLength, cornerThickness)), color);
        handle.DrawRect(UIBox2.FromDimensions(topLeft + new Vector2(boxSize.X - cornerThickness + cornerOffset, boxSize.Y - cornerLength + cornerOffset), new Vector2(cornerThickness, cornerLength)), color);

        // Центральна частинка
        var diamondSize = 5f * uiScale;
        handle.DrawRect(UIBox2.FromDimensions(screenPos.Position - new Vector2(diamondSize / 2, diamondSize / 2), new Vector2(diamondSize, diamondSize)), color);
    }
}
