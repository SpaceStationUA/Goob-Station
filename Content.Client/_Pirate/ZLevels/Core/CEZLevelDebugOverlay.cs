/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Client._Pirate.ZLevels.Core;

public sealed class CEZLevelDebugOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    private readonly CESharedZLevelsSystem _zLevels = default!;
    private readonly SharedTransformSystem _transform = default!;
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly Font? _font;

    public CEZLevelDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _zLevels = _entityManager.System<CESharedZLevelsSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();

        try
        {
            _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ce.zlevel.debug").Error($"Failed to load debug overlay font: {e}");
            _font = null;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_font is null)
            return;

        foreach (var uid in _zLevels.ActiveBodies)
        {
            if (!_entityManager.TryGetComponent<CEZPhysicsComponent>(uid, out var zPhys) ||
                !_entityManager.TryGetComponent<TransformComponent>(uid, out var xform))
            {
                continue;
            }

            if (xform.ParentUid != xform.MapUid && xform.ParentUid != xform.GridUid)
                continue;

            var worldPos = _transform.GetWorldPosition(uid);
            var screenPos = args.ViewportControl?.WorldToScreen(worldPos) ?? Vector2.Zero;

            var localPos = MathF.Round(zPhys.LocalPosition, 2);
            var groundDis = MathF.Round(zPhys.LocalPosition - zPhys.CurrentGroundHeight, 2);
            var velocity = MathF.Round(zPhys.Velocity, 2);
            var sticky = zPhys.CurrentStickyGround;

            var depthText = $"ZLocalHeight: {localPos}\nDistance to ground: {groundDis}\nVelocity: {velocity}\nSticky: {sticky}";

            args.ScreenHandle.DrawString(_font, screenPos, depthText, Color.White);
        }
    }
}

public sealed class CEShowZLevelDebugCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    public override string Command => "showzleveldebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<CEZLevelDebugOverlay>())
            _overlayManager.RemoveOverlay<CEZLevelDebugOverlay>();
        else
            _overlayManager.AddOverlay(new CEZLevelDebugOverlay());
    }
}
