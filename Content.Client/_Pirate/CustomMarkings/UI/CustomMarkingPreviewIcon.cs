// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Clickable;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// Preview of a whole marking: every one of its sprites stacked in draw order, each tinted with its own
/// layer colour. Markings are routinely split across sprites that are only drawn from some angles, so the
/// icon also falls back to a direction the marking is actually visible in rather than showing an empty box.
/// </summary>
public sealed class CustomMarkingPreviewIcon : Control
{
    /// <summary>Directions tried when the previewed one is empty, front first, back last.</summary>
    private static readonly Direction[] Fallbacks =
    {
        Direction.South, Direction.East, Direction.West, Direction.North,
    };

    // Populating a body part builds hundreds of these, each probing several sprites and directions,
    // so the two managers are resolved once rather than per probe.
    private static SpriteSystem? _spriteSystem;
    private static IClickMapManager? _clickMaps;

    private static SpriteSystem Sprites =>
        _spriteSystem ??= IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();

    private static IClickMapManager ClickMaps => _clickMaps ??= IoCManager.Resolve<IClickMapManager>();

    private readonly IReadOnlyList<SpriteSpecifier> _sprites;
    private readonly List<TextureRect> _layers = new();

    public CustomMarkingPreviewIcon(IReadOnlyList<SpriteSpecifier>? sprites, Direction direction, float scale = 2f, float size = 64f)
    {
        _sprites = sprites ?? Array.Empty<SpriteSpecifier>();
        MinSize = new Vector2(size, size);
        HorizontalAlignment = HAlignment.Center;

        foreach (var _ in _sprites)
        {
            var layer = new TextureRect
            {
                TextureScale = new Vector2(scale, scale),
                Stretch = TextureRect.StretchMode.KeepCentered,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };
            _layers.Add(layer);
            AddChild(layer);
        }

        SetDirection(direction);
    }

    /// <summary>Colour per sprite layer; layers past the end of the list are left white.</summary>
    public void SetColors(IReadOnlyList<Color>? colors)
    {
        for (var i = 0; i < _layers.Count; i++)
            _layers[i].ModulateSelfOverride = colors != null && i < colors.Count ? colors[i] : Color.White;
    }

    /// <summary>Single colour for every layer, for tiles that are not applied yet.</summary>
    public Color Tint
    {
        set
        {
            foreach (var layer in _layers)
                layer.ModulateSelfOverride = value;
        }
    }

    /// <summary>Draws the marking facing the given direction, or the nearest one it is not empty in.</summary>
    public void SetDirection(Direction direction)
    {
        var shown = VisibleDirection(_sprites, direction);
        for (var i = 0; i < _layers.Count; i++)
            _layers[i].Texture = FrameFor(_sprites[i], shown);
    }

    /// <summary>
    /// The given direction if any sprite draws something in it, otherwise the first fallback that does.
    /// Markings that are empty from every angle (the "none" and "bald" options) keep the asked-for direction.
    /// </summary>
    public static Direction VisibleDirection(IReadOnlyList<SpriteSpecifier> sprites, Direction preferred)
    {
        if (AnyVisible(sprites, preferred))
            return preferred;

        foreach (var dir in Fallbacks)
        {
            if (dir != preferred && AnyVisible(sprites, dir))
                return dir;
        }

        return preferred;
    }

    /// <summary>Whether any of the sprites draws something in the given direction.</summary>
    public static bool AnyVisible(IReadOnlyList<SpriteSpecifier> sprites, Direction direction)
    {
        foreach (var sprite in sprites)
        {
            if (Visible(sprite, direction))
                return true;
        }

        return false;
    }

    private static bool Visible(SpriteSpecifier sprite, Direction direction)
    {
        // Only RSI states carry per-direction frames; a flat texture always draws.
        if (Sprites.RsiStateLike(sprite) is not RSI.State state)
            return true;

        // Pirate: use the local exact pixel sampler instead of Wolfgate's click-map helper.
        var facing = direction.Convert(state.RsiDirections);
        for (var y = 0; y < state.RSI.Size.Y; y++)
        {
            for (var x = 0; x < state.RSI.Size.X; x++)
            {
                if (ClickMaps.IsOpaque(state.RSI, state.StateId, facing, 0, new Vector2i(x, y)))
                    return true;
            }
        }

        return false;
    }

    /// <summary>First frame of a sprite for a direction; sprites without directions give their only frame.</summary>
    public static Texture FrameFor(SpriteSpecifier sprite, Direction direction)
    {
        return Sprites.RsiStateLike(sprite).TextureFor(direction);
    }
}
