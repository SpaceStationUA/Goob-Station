// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Crayon;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._Pirate.Crayon;

public sealed class DecalPreviewSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly DecalRotationSystem _rotation = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    private DecalPreviewOverlay? _preview;

    public override void Initialize()
    {
        base.Initialize();

        _preview = new DecalPreviewOverlay();
        _overlay.AddOverlay(_preview);

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.EditorRotateObject,
                new PointerInputCmdHandler(HandleRotate, outsidePrediction: true),
                typeof(PlacementManager))
            .Register<DecalPreviewSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<DecalPreviewOverlay>();
        // Pirate: the combined preview owns a texture for the player's custom symbol.
        _preview?.Dispose();
        _preview = null;
        CommandBinds.Unregister<DecalPreviewSystem>();
    }

    private bool HandleRotate(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (_placement.IsActive
            || session?.AttachedEntity is not { } user
            || !_rotation.CanRotate(_hands.GetActiveItem(user)))
            return false;

        RaisePredictiveEvent(new DecalRotateRequestEvent());
        return true;
    }
}
