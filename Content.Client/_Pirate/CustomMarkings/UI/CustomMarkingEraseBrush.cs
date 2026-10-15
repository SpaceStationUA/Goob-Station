// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// What a canvas draws a body's layers with to leave out the pixels a marking erases. In a round the sprite system
/// feeds the erase shader its mask; a canvas draws the layers itself, so it feeds the same shader here: one facing
/// of a mask as a small texture, and a copy of the shader for each size of sprite, set to where the mask lies over
/// a sprite of that size.
/// </summary>
internal sealed class CustomMarkingEraseBrush : IDisposable
{
    private const int Frame = CustomMarkingRules.FrameSize;

    /// <summary>A clear rim around the mask: a sprite larger than the body samples it past the mask's edge.</summary>
    private const int Rim = 1;

    private const int Side = Frame + Rim * 2;
    private const int FacingBytes = CustomMarkingRules.EraseBytes / CustomMarkingRules.Facings;

    private readonly IClyde _clyde;
    private readonly ShaderPrototype _prototype;
    private readonly Dictionary<Vector2i, ShaderInstance> _shaders = new();
    private readonly byte[] _shown = new byte[FacingBytes];
    private OwnedTexture? _texture;
    private int _facing = -1;

    public CustomMarkingEraseBrush()
    {
        _clyde = IoCManager.Resolve<IClyde>();
        _prototype = IoCManager.Resolve<IPrototypeManager>().Index(CustomMarkingSystem.EraseShader);
    }

    /// <summary>
    /// Sets what is erased: one facing of a mask. Returns false, leaving nothing to draw with, when the mask is
    /// null or holds nothing of the facing.
    /// </summary>
    public bool Set(byte[]? mask, int facing)
    {
        if (mask is not { Length: CustomMarkingRules.EraseBytes } || !CustomMarkingErase.Any(mask, facing))
            return false;

        var bits = mask.AsSpan(facing * FacingBytes, FacingBytes);
        if (_texture != null && _facing == facing && bits.SequenceEqual(_shown))
            return true;

        bits.CopyTo(_shown);
        _facing = facing;

        using var image = new Image<Rgba32>(Side, Side);
        for (var y = 0; y < Frame; y++)
        {
            for (var x = 0; x < Frame; x++)
            {
                if (CustomMarkingErase.Get(mask, facing, x, y))
                    image[x + Rim, y + Rim] = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            }
        }

        _texture?.Dispose();
        _texture = _clyde.LoadTextureFromImage(image, "wf-custom-marking-erase");
        foreach (var shader in _shaders.Values)
        {
            shader.SetParameter(CustomMarkingSystem.EraseMaskParameter, _texture);
        }

        return true;
    }

    /// <summary>
    /// The shader to draw a layer's texture with. Sprites are drawn centred on the body, so one of another size
    /// has the mask in its middle. Each size has a shader of its own: what a shader is set to only reaches the
    /// screen when the frame's drawing is sent off, so one shader can't be set differently for two sprites.
    /// </summary>
    public ShaderInstance For(Vector2i size)
    {
        if (_shaders.TryGetValue(size, out var shader))
            return shader;

        shader = _prototype.InstanceUnique();

        // The mask's place in its texture for the corners of the sprite: first the top left, then the bottom
        // right, as a control's quad runs. A texture's rows count from the bottom.
        var overX = (size.X - Frame) / 2f;
        var overY = (size.Y - Frame) / 2f;
        shader.SetParameter(CustomMarkingSystem.EraseUVParameter, new Vector4(
            (Rim - overX) / Side,
            (Side - Rim + overY) / Side,
            (Side - Rim + overX) / Side,
            (Rim - overY) / Side));

        if (_texture != null)
            shader.SetParameter(CustomMarkingSystem.EraseMaskParameter, _texture);

        return _shaders[size] = shader;
    }

    public void Dispose()
    {
        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _shaders.Clear();
        _texture?.Dispose();
        _texture = null;
        _facing = -1;
    }
}
