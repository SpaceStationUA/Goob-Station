using Content.Client.Parallax;
using Content.Shared._FarHorizons.StarSystem;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.StarSystem;

/// <summary>
/// Legacy world-space star renderer. The parallax planet overlay now draws the star through
/// the same distance compression as the planets (so it recedes while flying away), so this
/// overlay stays registered (and toggleable with the rest of the system) but never draws.
/// </summary>
public sealed class StarOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public StarOverlay(IEntityManager entMan, IPrototypeManager protoMan)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _entMan = entMan;
        _protoMan = protoMan;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // The parallax planet overlay renders the star with the planets; skip the fixed-size world draw.
        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
    }

    public void ResetShader()
    {
    }
}
