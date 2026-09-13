// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Fullscreen art panel shown while the client is loading into a round.
///     Draws the cover-cropped art so it hands off seamlessly to the world-space
///     wipe overlay (which samples the same art the same way) once gameplay starts.
/// </summary>
public sealed class RoundWipeUiPanel : Control
{
    private readonly Texture _art;

    public RoundWipeUiPanel(Texture art)
    {
        _art = art;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Size.X <= 0 || Size.Y <= 0)
            return;

        // compute cover-fit: crop source rect to match the screen aspect ratio
        var texSize = (Vector2) _art.Size;
        var texAspect = texSize.X / texSize.Y;
        var screenAspect = Size.X / Size.Y;
        UIBox2 src = UIBox2.FromDimensions(0, 0, 1, 1);
        if (texAspect > screenAspect)
        {
            var visible = screenAspect / texAspect;
            var x0 = (1 - visible) / 2;
            src = UIBox2.FromDimensions(x0, 0, visible, 1);
        }
        else
        {
            var visible = texAspect / screenAspect;
            var y0 = (1 - visible) / 2;
            src = UIBox2.FromDimensions(0, y0, 1, visible);
        }

        var dest = UIBox2.FromDimensions(0, 0, Size.X, Size.Y);
        handle.DrawTextureRectRegion(_art, dest, src);
    }
}
