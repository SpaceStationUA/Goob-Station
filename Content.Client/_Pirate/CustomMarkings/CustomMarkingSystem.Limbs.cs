// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Client.Clickable;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.CustomMarkings;

// A marking is only drawn on the body that wears it, on the body's other markings such as a tail or ears, and on a
// small margin around them. Each of its pixels belongs to what it lies on: a body part that is hidden, such as a lost
// limb, takes its pixels along, and so does a tail or a pair of ears a helmet hides. Every client cuts the art to the
// wearer's own outline as it draws, so nothing a player uploads can reach past that. Art drawn on the body that wears
// it already fits, and is drawn as it is.
public sealed partial class CustomMarkingSystem
{
    [Dependency] private IClickMapManager _clickMap = default!;
    [Dependency] private MarkingManager _markings = default!;

    private const int MaxCutArt = 512;
    private const int MaxDecodedArt = 128;
    private const int MaxSections = 256;

    /// <summary>A section map numbers what it holds in a byte, so only so many marking sprites can have a section.</summary>
    private const int MaxExtras = 200;

    private static readonly byte[] NothingSolid = new byte[CustomMarkingRules.EraseBytes];

    private static readonly MarkingCategories[] HairCategories = { MarkingCategories.Hair, MarkingCategories.FacialHair };

    /// <summary>The body part layers that art can lie on.</summary>
    private static readonly HumanoidVisualLayers[] Limbs =
    {
        HumanoidVisualLayers.Chest,
        // Pirate: these body parts have separate base sprites in our species.
        HumanoidVisualLayers.Groin,
        HumanoidVisualLayers.Face,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    };

    /// <summary>
    /// What a body shows of a marking's art: the sprite to draw, null when nothing of the drawing is left, and the
    /// pixels it draws solidly, as a <see cref="CustomMarkingErase"/> mask.
    /// </summary>
    private sealed record Shown(RSI? Rsi, byte[] Solid);

    /// <summary>What is shown of art, by what it was cut from and for which body; null for art that can't be read.</summary>
    private readonly Dictionary<string, Shown?> _cut = new();

    /// <summary>Fetched art read back from what was stored, the first time it has to be cut.</summary>
    private readonly Dictionary<string, CustomMarkingArt> _decoded = new();

    /// <summary>What owns each pixel, by body: the sprites it is made of and how many of its parts lie under the art.</summary>
    private readonly Dictionary<string, byte[]> _sections = new();

    /// <summary>How a layer is drawn, which decides whether erasing can be applied to it.</summary>
    private enum Shading : byte
    {
        /// <summary>Lit like anything else.</summary>
        Lit,

        /// <summary>Drawn unshaded, as glowing eyes are.</summary>
        Unshaded,

        /// <summary>Drawn with a shader of its own, which erasing leaves alone.</summary>
        Other,
    }

    /// <summary>
    /// One sprite layer art can lie on: a body part, or a marking the body shows. <see cref="Key"/> is what the
    /// sprite maps the layer under. <see cref="Offset"/> is where the art's corner falls on a sprite of another
    /// size, as sprites are drawn centred.
    /// </summary>
    private readonly record struct Part(
        int Index,
        object Key,
        string Name,
        RSI Rsi,
        RSI.StateId State,
        bool Facings,
        bool Hidden,
        Shading Shading,
        Vector2i Offset)
    {
        public bool IsOpaque(IClickMapManager clickMap, int facing, int x, int y)
        {
            var direction = Facings ? (RsiDirection) facing : RsiDirection.South;
            return clickMap.IsOpaque(Rsi, State, direction, 0, new Vector2i(x, y) + Offset);
        }
    }

    /// <summary>The body part layers a sprite has, lowest drawn first. A hidden one is still listed.</summary>
    private List<Part> FindLimbs(Entity<SpriteComponent?> sprite)
    {
        var limbs = new List<Part>();
        if (sprite.Comp is not { } comp)
            return limbs;

        foreach (var limb in Limbs)
        {
            if (_sprite.LayerMapTryGet(sprite, limb, out var index, false) && TryGetPart(comp, index, limb, limb.ToString(), out var part))
                limbs.Add(part);
        }

        limbs.Sort((a, b) => a.Index.CompareTo(b.Index));
        return limbs;
    }

    /// <summary>
    /// The sprites of the markings a body shows, such as a tail, ears or hair, lowest drawn first. One that is
    /// hidden, by a helmet say, is left out: art has nothing to lie on there.
    /// </summary>
    private List<Part> FindExtras(Entity<SpriteComponent?> sprite)
    {
        var extras = new List<Part>();
        if (sprite.Comp is not { } comp || !TryComp<HumanoidAppearanceComponent>(sprite, out var humanoid))
            return extras;

        foreach (var worn in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in worn)
            {
                if (!_markings.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var specifier in prototype.Sprites)
                {
                    if (extras.Count >= MaxExtras)
                        break;

                    if (specifier is not SpriteSpecifier.Rsi drawn)
                        continue;

                    // The humanoid system keys each marking sprite's layer this way.
                    var key = $"{prototype.ID}-{drawn.RsiState}";
                    if (_sprite.LayerMapTryGet(sprite, key, out var index, false)
                        && TryGetPart(comp, index, key, prototype.ID, out var part)
                        && !part.Hidden)
                        extras.Add(part);
                }
            }
        }

        extras.Sort((a, b) => a.Index.CompareTo(b.Index));
        return extras;
    }

    /// <summary>
    /// Lists the layers of a sprite that draw its hair and facial hair, by index, for a view that leaves them out
    /// to show what is under them.
    /// </summary>
    public void GetHairLayers(Entity<SpriteComponent?> sprite, HashSet<int> indices)
    {
        indices.Clear();
        if (!TryComp<HumanoidAppearanceComponent>(sprite, out var humanoid))
            return;

        foreach (var category in HairCategories)
        {
            if (!humanoid.MarkingSet.Markings.TryGetValue(category, out var worn))
                continue;

            foreach (var marking in worn)
            {
                if (!_markings.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var specifier in prototype.Sprites)
                {
                    if (specifier is SpriteSpecifier.Rsi drawn
                        && _sprite.LayerMapTryGet(sprite, $"{prototype.ID}-{drawn.RsiState}", out var index, false))
                        indices.Add(index);
                }
            }
        }
    }

    private static bool TryGetPart(SpriteComponent sprite, int index, object key, string name, out Part part)
    {
        part = default;
        if (sprite[index] is not SpriteComponent.Layer layer
            || layer.ActualRsi is not { } rsi
            || !rsi.TryGetState(layer.State, out var state))
            return false;

        var shading = Shading.Other;
        if (layer.ShaderPrototype == null)
            shading = layer.Shader == null ? Shading.Lit : Shading.Other;
        else if (layer.ShaderPrototype == SpriteSystem.UnshadedId || layer.ShaderPrototype == EraseShaderUnshaded)
            shading = Shading.Unshaded;
        else if (layer.ShaderPrototype == EraseShader)
            shading = Shading.Lit;

        var offset = (rsi.Size - new Vector2i(CustomMarkingRules.FrameSize, CustomMarkingRules.FrameSize)) / 2;
        part = new Part(index, key, name, rsi, layer.State, state.RsiDirections != RsiDirectionType.Dir1, !layer.Visible, shading, offset);
        return true;
    }

    /// <summary>
    /// What owns each pixel of a marking drawn at a depth on a body, in the form <see cref="CustomMarkingSections"/>
    /// builds: limbs first, then extras. Bodies made of the same sprites share one map.
    /// </summary>
    /// <param name="depth">The layer index the marking is added at; parts at or above it draw over the art.</param>
    /// <param name="body">A name for the body as far as the map goes, for keying what is made from it.</param>
    private byte[] SectionsFor(List<Part> limbs, List<Part> extras, int? depth, out string body)
    {
        var under = 0;
        var key = new StringBuilder();
        foreach (var limb in limbs)
        {
            if (depth is not { } above || limb.Index < above)
                under++;

            Name(key, limb);
        }

        key.Append(under).Append('#');
        foreach (var extra in extras)
        {
            Name(key, extra);
        }

        body = key.ToString();
        if (_sections.TryGetValue(body, out var known))
            return known;

        if (_sections.Count >= MaxSections)
            _sections.Clear();

        return _sections[body] = CustomMarkingSections.Build(limbs.Count, under, extras.Count, (part, facing, x, y) =>
            (part < limbs.Count ? limbs[part] : extras[part - limbs.Count]).IsOpaque(_clickMap, facing, x, y));

        static void Name(StringBuilder key, Part part)
        {
            key.Append(part.Name).Append('=').Append(part.Rsi.Path.ToString()).Append(':').Append(part.State.Name).Append('|');
        }
    }

    /// <summary>
    /// The pixels a marking with a placement can use on a body: a map as <see cref="CustomMarkingSections"/> builds,
    /// where <see cref="CustomMarkingSections.None"/> is out of reach. Null for a sprite with no body parts.
    /// </summary>
    public byte[]? GetSections(Entity<SpriteComponent?> sprite, CustomMarkingPlacement placement)
    {
        var limbs = FindLimbs(sprite);
        return limbs.Count == 0 ? null : SectionsFor(limbs, FindExtras(sprite), GetLayerIndex(sprite, placement), out _);
    }

    /// <summary>
    /// What to draw for a marking on this body: the drawing without whatever lies out of the body's reach or on a
    /// hidden body part. That is the whole drawing when nothing does. Null for a sprite with no body parts, and
    /// for art that can't be read.
    /// </summary>
    /// <param name="depth">The layer index the marking will be added at.</param>
    private Shown? ArtFor(Entity<SpriteComponent?> sprite, CustomMarking marking, RSI whole, int? depth)
    {
        var limbs = FindLimbs(sprite);
        if (limbs.Count == 0)
            return null;

        var sections = SectionsFor(limbs, FindExtras(sprite), depth, out var body);
        var key = new StringBuilder(marking.Hash).Append('|').Append(body).Append('|');
        foreach (var limb in limbs)
        {
            key.Append(limb.Hidden ? '-' : '+');
        }

        var cutKey = key.ToString();
        if (_cut.TryGetValue(cutKey, out var known))
            return known;

        if (_cut.Count >= MaxCutArt)
            _cut.Clear();

        return _cut[cutKey] = Cut(marking.Hash, whole, sections, limbs);
    }

    private Shown? Cut(string hash, RSI whole, byte[] sections, List<Part> limbs)
    {
        // Art that can't be read can't be shown to fit, so it isn't shown.
        if (!TryDecode(hash, out var art))
            return null;

        var cut = art.Without((facing, x, y) =>
        {
            // Only a limb can be listed and hidden; what else the map numbers was showing when it was made.
            var owner = sections[CustomMarkingSections.Index(facing, x, y)];
            return owner == CustomMarkingSections.None || owner <= limbs.Count && limbs[owner - 1].Hidden;
        });

        if (cut.IsBlank())
            return new Shown(null, NothingSolid);

        if (cut.SamePixels(art))
            return new Shown(whole, cut.Solid());

        // Named after itself, so the same cut is one RSI however it was reached, on this connection or the last.
        var name = $"{hash}-{cut.Fingerprint()}";
        if (!_resources.Has(name))
            _resources.Store(name, cut.ToPng(), cut.GetFrameTimes());

        try
        {
            return new Shown(_resCache.GetResource<RSIResource>(CustomMarkingResources.PathFor(name), false).RSI, cut.Solid());
        }
        catch (Exception e)
        {
            Log.Error($"Custom marking art {name} didn't load: {e.Message}");
            return new Shown(null, NothingSolid);
        }
    }

    /// <summary>
    /// Where a mirror stands to split a body down its middle in one facing, read off the torso: the sum of its
    /// leftmost and rightmost columns, in the form <see cref="CustomMarkingSketch.MirrorAxis"/> takes. Null for a
    /// body with no torso sprite.
    /// </summary>
    public int? MirrorAxis(Entity<SpriteComponent?> sprite, int facing)
    {
        if (sprite.Comp is not { } comp
            || !_sprite.LayerMapTryGet(sprite, HumanoidVisualLayers.Chest, out var index, false)
            || !TryGetPart(comp, index, HumanoidVisualLayers.Chest, nameof(HumanoidVisualLayers.Chest), out var torso))
            return null;

        int? left = null;
        var right = 0;
        for (var x = 0; x < CustomMarkingRules.FrameSize; x++)
        {
            for (var y = 0; y < CustomMarkingRules.FrameSize; y++)
            {
                if (!torso.IsOpaque(_clickMap, facing, x, y))
                    continue;

                left ??= x;
                right = x;
                break;
            }
        }

        return left + right;
    }

    private bool TryDecode(string hash, [NotNullWhen(true)] out CustomMarkingArt? art)
    {
        if (_decoded.TryGetValue(hash, out art))
            return true;

        if (!TryReadArt(hash, out var read))
            return false;

        if (_decoded.Count >= MaxDecodedArt)
            _decoded.Clear();

        art = _decoded[hash] = read;
        return true;
    }
}
