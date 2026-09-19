using System.Numerics;
using Content.Shared._Pirate.Silicons.IPC;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Silicons.IPC;

/// <summary>
/// CRT-style "screen vision" overlay, ported from starcup PR #911
/// (shader "Screen Vision" by banwarfarms, CC0).
/// </summary>
public sealed partial class ScreenVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private static readonly ProtoId<ShaderPrototype> ScreenVision = "ScreenVision";

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _ultraVisionShader;

    public ScreenVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        // instance that comes with pre-set prototype params is immutable
        _ultraVisionShader = _prototypeManager.Index(ScreenVision).Instance().Duplicate();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { Valid: true } player
            || !_entityManager.HasComponent<ScreenVisionComponent>(player))
        {
            return false;
        }

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _ultraVisionShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_ultraVisionShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null); // important - as of writing, construction overlay breaks without this
    }
}
