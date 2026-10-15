// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.CustomMarkings;

// A marking can erase parts of the body under it, so that what is drawn takes the body's place instead of lying on
// top of it. The body's own layers are left as they are: each one the mask touches is drawn through a shader that
// drops the masked pixels, fed by a layer that holds the mask and is never drawn itself, the way clothing is fitted
// to a species with a displacement map. Clothing is not a body layer, so nothing worn is ever erased.
//
// Erasing must leave a body in sight. Every client checks that for the wearer's own body as it draws, and where
// too little would be left, nothing is erased at all.
public sealed partial class CustomMarkingSystem
{
    /// <summary>The shader a body layer is drawn with while part of it is erased.</summary>
    public static readonly ProtoId<ShaderPrototype> EraseShader = "WolfgateCustomMarkingErase";

    /// <summary>The same for a layer that is drawn unshaded.</summary>
    public static readonly ProtoId<ShaderPrototype> EraseShaderUnshaded = "WolfgateCustomMarkingEraseUnshaded";

    /// <summary>The shaders' parameters: the texture holding the mask, and where the mask lies in it.</summary>
    public const string EraseMaskParameter = "eraseMask";

    public const string EraseUVParameter = "eraseUV";

    /// <summary>Mask layer keys are numbered, so this is also the most layers of one body that can be erased from.</summary>
    private const int MaxEraseLayers = 48;

    private const int MaxEraseSprites = 256;
    private const int MaxBodyMasks = 256;
    private const int MaxTouches = 4096;
    private const int MaxEraseMasks = 512;

    /// <summary>Mask sprites by the mask's fingerprint and the size of the sprite they are for; null for one that didn't load.</summary>
    private readonly Dictionary<string, RSI?> _eraseSprites = new();

    /// <summary>The pixels of a body's own parts, by the sprites those are.</summary>
    private readonly Dictionary<string, byte[]> _bodyMasks = new();

    /// <summary>Whether a mask touches a sprite at all, by both.</summary>
    private readonly Dictionary<string, bool> _touches = new();

    /// <summary>Erase masks of fetched art by hash; null for art that has none.</summary>
    private readonly Dictionary<string, byte[]?> _eraseMasks = new();

    /// <summary>Whether the server lets markings erase the body.</summary>
    public bool EraseEnabled => _cfg.GetCVar(CustomMarkingCVars.EraseBody);

    /// <summary>The key of one of the layers that hold a body's erase mask.</summary>
    public static string EraseKey(int index)
    {
        return $"wf-custom-marking-erase-{index}";
    }

    /// <summary>The body pixels a marking erases, for art that <see cref="TryGetArt"/> has returned. False when it erases none.</summary>
    public bool TryGetErase(string hash, [NotNullWhen(true)] out byte[]? erase)
    {
        erase = null;
        if (!TryGetArt(hash, out _))
            return false;

        if (_eraseMasks.TryGetValue(hash, out erase))
            return erase != null;

        if (_eraseMasks.Count >= MaxEraseMasks)
            _eraseMasks.Clear();

        _resources.TryGetErase(hash, out erase);
        _eraseMasks[hash] = erase;
        return erase != null;
    }

    /// <summary>
    /// The pixels of a body's own parts as a <see cref="CustomMarkingErase"/> mask: what erasing must leave enough
    /// of. Null for a sprite with no body parts.
    /// </summary>
    public byte[]? GetBodyMask(Entity<SpriteComponent?> sprite)
    {
        var limbs = FindLimbs(sprite);
        return limbs.Count == 0 ? null : BodyMask(limbs);
    }

    /// <summary>Lists the layers of a sprite that erasing applies to, by index: the body's parts, its eyes and its markings.</summary>
    public void GetErasable(Entity<SpriteComponent?> sprite, HashSet<int> indices)
    {
        indices.Clear();
        foreach (var part in FindErasable(sprite))
        {
            if (part.Shading != Shading.Other)
                indices.Add(part.Index);
        }
    }

    /// <summary>Every layer erasing applies to: the body's parts, its eyes and the sprites of the markings it shows.</summary>
    private List<Part> FindErasable(Entity<SpriteComponent?> sprite)
    {
        var parts = FindLimbs(sprite);
        if (sprite.Comp is { } comp
            && _sprite.LayerMapTryGet(sprite, HumanoidVisualLayers.Eyes, out var index, false)
            && TryGetPart(comp, index, HumanoidVisualLayers.Eyes, nameof(HumanoidVisualLayers.Eyes), out var eyes))
            parts.Add(eyes);

        parts.AddRange(FindExtras(sprite));
        return parts;
    }

    private bool TryGetLayer(Entity<SpriteComponent?> sprite, object key, out int index)
    {
        index = 0;
        return key switch
        {
            Enum layer => _sprite.LayerMapTryGet(sprite, layer, out index, false),
            string layer => _sprite.LayerMapTryGet(sprite, layer, out index, false),
            _ => false,
        };
    }

    /// <summary>Takes a body's mask layers off, and draws the layers they masked the way those were drawn before.</summary>
    private void RemoveErase(Entity<SpriteComponent?> sprite)
    {
        if (sprite.Comp is not { } comp)
            return;

        for (var i = 0; i < MaxEraseLayers; i++)
        {
            var key = EraseKey(i);
            if (!_sprite.LayerMapTryGet(sprite, key, out var index, false))
                continue;

            // The layer it masked may be gone by now: a marking's layers are remade whenever the body changes.
            if (comp[index] is SpriteComponent.Layer { CopyToShaderParameters: { } copy }
                && TryGetLayer(sprite, copy.LayerKey, out var target)
                && comp[target] is SpriteComponent.Layer masked)
            {
                if (masked.ShaderPrototype == EraseShaderUnshaded)
                    comp.LayerSetShader(target, SpriteSystem.UnshadedId.Id);
                else if (masked.ShaderPrototype == EraseShader)
                    comp.LayerSetShader(target, (ShaderInstance?) null);
            }

            _sprite.RemoveLayer(sprite, key, false);
        }
    }

    /// <summary>Hides the pixels of a body that its markings erase, if enough of it is left in sight.</summary>
    /// <param name="erase">The pixels erased by everything the body wears.</param>
    /// <param name="covered">The pixels the markings shown on it draw over solidly.</param>
    private void ApplyErase(Entity<SpriteComponent?> sprite, byte[] erase, byte[] covered)
    {
        if (sprite.Comp is not { } comp || !EraseEnabled || !CustomMarkingErase.Any(erase))
            return;

        var limbs = FindLimbs(sprite);
        if (limbs.Count == 0 || !CustomMarkingErase.LeavesEnough(BodyMask(limbs), erase, covered))
            return;

        var fingerprint = CustomMarkingErase.Fingerprint(erase);
        var added = 0;
        foreach (var part in FindErasable(sprite))
        {
            if (added >= MaxEraseLayers)
                break;

            if (part.Hidden
                || part.Shading == Shading.Other
                || !Touches(part, erase, fingerprint)
                || EraseSprite(erase, fingerprint, part.Rsi.Size) is not { } mask
                // Looked up again each time: every mask layer goes in under its part and moves the ones above.
                || !TryGetLayer(sprite, part.Key, out var index))
                continue;

            var key = EraseKey(added);
            var layer = _sprite.AddRsiLayer(sprite, CustomMarkingResources.State, mask, index);
            _sprite.LayerMapSet(sprite, key, layer);
            if (comp[layer] is not SpriteComponent.Layer holder || !TryGetLayer(sprite, part.Key, out var target))
            {
                _sprite.RemoveLayer(sprite, key, false);
                continue;
            }

            // Not drawn: it only hands its texture, turned to the facing shown, to the shader of the part.
            holder.CopyToShaderParameters = new SpriteComponent.CopyToShaderParameters(part.Key)
            {
                ParameterTexture = EraseMaskParameter,
                ParameterUV = EraseUVParameter,
            };
            comp.LayerSetShader(target, (part.Shading == Shading.Unshaded ? EraseShaderUnshaded : EraseShader).Id);
            added++;
        }
    }

    /// <summary>Whether a mask erases any pixel a sprite draws.</summary>
    private bool Touches(Part part, byte[] erase, string fingerprint)
    {
        var key = $"{fingerprint}|{part.Rsi.Path}:{part.State.Name}";
        if (_touches.TryGetValue(key, out var known))
            return known;

        if (_touches.Count >= MaxTouches)
            _touches.Clear();

        var touches = false;
        for (var facing = 0; facing < CustomMarkingRules.Facings && !touches; facing++)
        {
            if (!CustomMarkingErase.Any(erase, facing))
                continue;

            for (var y = 0; y < CustomMarkingRules.FrameSize && !touches; y++)
            {
                for (var x = 0; x < CustomMarkingRules.FrameSize && !touches; x++)
                {
                    touches = CustomMarkingErase.Get(erase, facing, x, y) && part.IsOpaque(_clickMap, facing, x, y);
                }
            }
        }

        return _touches[key] = touches;
    }

    /// <summary>A mask as a sprite of four facings, the size of the sprites it is to mask.</summary>
    private RSI? EraseSprite(byte[] erase, string fingerprint, Vector2i size)
    {
        var name = $"erase-{fingerprint}-{size.X}x{size.Y}";
        if (_eraseSprites.TryGetValue(name, out var known))
            return known;

        if (_eraseSprites.Count >= MaxEraseSprites)
            _eraseSprites.Clear();

        if (!_resources.Has(name))
            _resources.Store(name, CustomMarkingErase.ToPng(erase, size.X, size.Y), size: size);

        try
        {
            return _eraseSprites[name] = _resCache.GetResource<RSIResource>(CustomMarkingResources.PathFor(name), false).RSI;
        }
        catch (Exception e)
        {
            Log.Error($"Custom marking erase mask {name} didn't load: {e.Message}");
            return _eraseSprites[name] = null;
        }
    }

    /// <summary>The pixels of the body parts that show, as a mask. Bodies made of the same sprites share one.</summary>
    private byte[] BodyMask(List<Part> limbs)
    {
        var key = new StringBuilder();
        foreach (var limb in limbs)
        {
            if (!limb.Hidden)
                key.Append(limb.Rsi.Path.ToString()).Append(':').Append(limb.State.Name).Append('|');
        }

        var body = key.ToString();
        if (_bodyMasks.TryGetValue(body, out var known))
            return known;

        if (_bodyMasks.Count >= MaxBodyMasks)
            _bodyMasks.Clear();

        var mask = new byte[CustomMarkingRules.EraseBytes];
        for (var facing = 0; facing < CustomMarkingRules.Facings; facing++)
        {
            for (var y = 0; y < CustomMarkingRules.FrameSize; y++)
            {
                for (var x = 0; x < CustomMarkingRules.FrameSize; x++)
                {
                    foreach (var limb in limbs)
                    {
                        if (limb.Hidden || !limb.IsOpaque(_clickMap, facing, x, y))
                            continue;

                        CustomMarkingErase.Set(mask, facing, x, y, true);
                        break;
                    }
                }
            }
        }

        return _bodyMasks[body] = mask;
    }
}
