// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Humanoid;
using Content.Goobstation.Shared.ModSuits.Components;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared._Pirate.Clothing.SuitWrap;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Clothing.SuitWrap;

// Marking updates reset shaders, so the wrap must be reapplied each frame.
public sealed class SuitWrapSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "PirateSuitWrap";
    private static readonly ProtoId<TagPrototype> EvaHelmetTag = "HelmetEVA";
    private const string OuterSlot = "outerClothing";
    private const string HeadSlot = "head";

    // Where each kind of marking attaches to a 32x32 mob, in pixels from the top-left of the frame.
    private static readonly Vector2 TailAnchor = new(16, 20);
    private static readonly Vector2 WingsAnchor = new(16, 13);
    private static readonly Vector2 HeadTopAnchor = new(16, 7);

    private readonly Dictionary<EntityUid, WrapState> _active = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _stale = new();

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var wearer in _active.Keys)
        {
            _stale.Add(wearer);
        }

        foreach (var wearer in _stale)
        {
            Clear(wearer);
        }

        _stale.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _seen.Clear();
        var query = EntityQueryEnumerator<SuitWrapComponent, ClothingComponent, TransformComponent>();
        while (query.MoveNext(out var suit, out var wrap, out var clothing, out var xform))
        {
            if (clothing.InSlot != OuterSlot || !IsSealed(suit))
                continue;

            var wearer = xform.ParentUid;
            if (!TryComp<HumanoidAppearanceComponent>(wearer, out var humanoid)
                || !TryComp<SpriteComponent>(wearer, out var sprite))
            {
                continue;
            }

            if (_active.TryGetValue(wearer, out var state) && (state.Suit != suit || state.Wrap != wrap))
            {
                Clear(wearer);
                state = null;
            }

            if (state == null)
            {
                state = new WrapState(suit, wrap);
                _active[wearer] = state;
            }

            _seen.Add(wearer);

            var headSealed = _inventory.TryGetSlotEntity(wearer, HeadSlot, out var head)
                && IsSuitHelmet(suit, head.Value);

            Apply((wearer, humanoid, sprite), state, headSealed);
        }

        foreach (var wearer in _active.Keys)
        {
            if (!_seen.Contains(wearer))
                _stale.Add(wearer);
        }

        foreach (var wearer in _stale)
        {
            Clear(wearer);
        }

        _stale.Clear();
    }

    // EVA suits and helmets are separate items with no link.
    private bool IsSuitHelmet(EntityUid suit, EntityUid head)
    {
        if (HasComp<ModPartComponent>(head) || HasComp<ModularSuitPartComponent>(head))
            return IsSealed(head);

        if (TryComp<ToggleableClothingComponent>(suit, out var toggleable))
        {
            IReadOnlyDictionary<EntityUid, string> deployed = toggleable.ClothingUids;
            if (deployed.ContainsKey(head))
                return true;
        }

        return _tag.HasTag(head, EvaHelmetTag);
    }

    // Hardsuits and EVA suits have no seal; wearing them closes them.
    private bool IsSealed(EntityUid uid)
    {
        if (TryComp<ModPartComponent>(uid, out var part))
            return part.Sealed;

        if (HasComp<ModularSuitPartComponent>(uid))
            return TryComp<ItemToggleComponent>(uid, out var toggle) && toggle.Activated;

        return true;
    }

    private void Apply(Entity<HumanoidAppearanceComponent, SpriteComponent> wearer, WrapState state, bool headSealed)
    {
        var humanoid = wearer.Comp1;
        var sprite = wearer.Comp2;

        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markings.TryGetMarking(marking, out var proto))
                    continue;

                Vector2 anchor;
                switch (proto.MarkingCategory)
                {
                    case MarkingCategories.Tail:
                        anchor = TailAnchor;
                        break;
                    case MarkingCategories.Wings:
                        anchor = WingsAnchor;
                        break;
                    case MarkingCategories.HeadTop when headSealed:
                        anchor = HeadTopAnchor;
                        break;
                    default:
                        continue;
                }

                // Under a sealed helmet the head top stays visible, as suit plates. Respect what the player hid
                // and what the species can never show.
                var reveal = proto.MarkingCategory == MarkingCategories.HeadTop
                    && marking.Visible
                    && !humanoid.PermanentlyHidden.Contains(proto.BodyPart)
                    && humanoid.BaseLayers.TryGetValue(proto.BodyPart, out var setting)
                    && setting.AllowsMarkings;

                foreach (var spriteSpec in proto.Sprites)
                {
                    if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerKey = $"{proto.ID}-{rsi.RsiState}";
                    if (!_sprite.LayerMapTryGet((wearer.Owner, sprite), layerKey, out var index, false))
                        continue;

                    var layer = sprite[index];
                    if (layer is not SpriteComponent.Layer spriteLayer)
                        continue;

                    if (reveal && !spriteLayer.Visible)
                        _sprite.LayerSetVisible((wearer.Owner, sprite), index, true);

                    var shader = GetShader(state, layerKey, spriteLayer, anchor);
                    if (shader != null && spriteLayer.Shader != shader)
                        sprite.LayerSetShader(index, shader, ShaderId);
                }
            }
        }
    }

    private ShaderInstance? GetShader(WrapState state, string layerKey, SpriteComponent.Layer layer, Vector2 anchor)
    {
        if (layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.State, out var rsiState))
            return null;

        var key = (layerKey, rsi.Path);
        if (state.Shaders.TryGetValue(key, out var cached))
            return cached;

        // All frames of an RSI share one grid in the atlas, so any frame gives its origin.
        var origin = rsiState.Frame0 is AtlasTexture atlas
            ? new Vector2(atlas.SubRegion.Left, atlas.SubRegion.Top)
            : Vector2.Zero;

        // Bigger frames are centred on the mob, like the sprite system draws them.
        var size = new Vector2(rsi.Size.X, rsi.Size.Y);
        var offset = (size - new Vector2(32, 32)) / 2f;

        var wrap = state.Wrap;
        var shader = _proto.Index(ShaderId).InstanceUnique();
        shader.SetParameter("frameOrigin", origin);
        shader.SetParameter("frameSize", size);
        shader.SetParameter("anchor", anchor + offset);
        for (var i = 0; i < 4; i++)
        {
            var color = wrap.Ramp.Count == 0 ? Color.Gray : wrap.Ramp[Math.Min(i, wrap.Ramp.Count - 1)];
            shader.SetParameter($"ramp{i}", ToVector(color));
        }

        shader.SetParameter("accentColor", ToVector(wrap.Accent));
        shader.SetParameter("seamPeriod", (float) Math.Max(2, wrap.SeamPeriod));
        shader.SetParameter("accentStrength", Math.Clamp(wrap.AccentStrength, 0f, 1f));

        state.Shaders[key] = shader;
        return shader;
    }

    private void Clear(EntityUid wearer)
    {
        if (!_active.Remove(wearer, out var state))
            return;

        foreach (var shader in state.Shaders.Values)
        {
            shader.Dispose();
        }

        if (!TerminatingOrDeleted(wearer)
            && TryComp<HumanoidAppearanceComponent>(wearer, out var humanoid)
            && TryComp<SpriteComponent>(wearer, out var sprite))
        {
            _humanoid.UpdateSprite((wearer, humanoid, sprite));
        }
    }

    private static Vector3 ToVector(Color color)
    {
        return new Vector3(color.R, color.G, color.B);
    }

    private sealed class WrapState(EntityUid suit, SuitWrapComponent wrap)
    {
        public readonly EntityUid Suit = suit;
        public readonly SuitWrapComponent Wrap = wrap;
        public readonly Dictionary<(string Layer, ResPath Rsi), ShaderInstance> Shaders = new();
    }
}
