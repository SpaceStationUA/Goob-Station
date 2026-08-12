using Content.Client._Shitmed.Medical.Surgery.Wounds;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Pirate.Client.Wetness;
using Content.Pirate.Shared.Feroxi;
using Content.Pirate.Shared.Wetness.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Pirate.Client.Feroxi;

/// <summary>
/// Hides an underwater Feroxi's body, leaving only their dorsal fin visible and pointed the way
/// they're swimming.
/// </summary>
/// <remarks>
/// This system hides layers by ALLOWLIST, and only ever hides layers whose restoration is guaranteed
/// by a system that recomputes them from live state:
///
///   1. humanoid base layers  -> restored by HumanoidAppearanceSystem.UpdateSprite
///   2. marking layers        -> restored by HumanoidAppearanceSystem.UpdateSprite
///   3. worn clothing layers  -> restored by ClientClothingSystem re-rendering every slot on an
///                               appearance update (it removes and re-adds the layers outright)
///   4. wound/bleed overlays  -> restored by WoundableVisualsSystem.SetWoundVisualsVisible, which
///                               recomputes them from current wound state
///   5. wetness droplets      -> restored from WetVisualsComponent presence, which is exactly when
///                               WetnessSystem keeps that layer alive and visible
///
/// Nothing else is touched. That is deliberate and is the whole design: every other layer on a
/// humanoid belongs to some other system, and there is no safe way to hand its state back.
/// Approaches that were tried and provably fail:
///
///   - Remembering hidden layers by INDEX. An appearance or marking refresh (speaking causes one)
///     rebuilds layers and shifts every index after the markings, so restoring by index wrote
///     Visible=true onto whatever inherited the slot. That is what switched on the creampie and
///     handcuff layers after surfacing.
///   - Remembering a layer's visibility and restoring it. The owning system can change its mind while
///     the layer is hidden and we cannot observe it doing so, because LayerSetVisible early-returns
///     when the value already matches. A stun ending mid-swim left the "stars" stuck on.
///   - Hiding everything and re-deriving on surface. Systems that add a layer once and never
///     re-assert it (WetnessSystem's droplets) have nothing to re-derive from, so their layer stayed
///     hidden forever.
///
/// Consequence to be aware of: layers outside those categories stay VISIBLE while underwater - notably
/// handcuffs (CuffableSystem drives them from networked component state, so there is nothing to
/// re-derive from), the typing indicator (deliberate - an above-water cue, like the status icons), and
/// appearance-driven effects such as fire and stun stars.
///
/// Do not widen the hide set unless the layer's owner provably recomputes visibility from live state.
/// If it doesn't, add a public re-assert to that owner first - that is what categories 4 and 5 did.
/// </remarks>
public sealed class FeroxiUnderwaterVisualsSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly WoundableVisualsSystem _woundVisuals = default!;

    /// <summary>
    /// The separable dorsal-fin states in the Feroxi tail_markings.rsi. The tail sprite itself has a
    /// second fin drawn into its art, which can't be split out without a dedicated sprite for it.
    /// </summary>
    private static readonly HashSet<string> FinStates =
    [
        "feroxi-dorsal",
        "feroxi-dorsal-tip",
        "feroxi-dorsal-stripes",
        "feroxi-tail-second-dorsal-tip",
    ];

    /// <summary>Who we are currently hiding. No per-layer state is kept - see the class remarks.</summary>
    private readonly HashSet<EntityUid> _underwater = new();

    public override void Initialize()
    {
        base.Initialize();

        // ComponentShutdown is already taken by SharedFeroxiUnderwaterSystem (which runs client-side too)
        // and only one subscriber per component/event pair is allowed, so these cover the same ground.
        SubscribeLocalEvent<FeroxiUnderwaterComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<FeroxiUnderwaterComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = AllEntityQuery<FeroxiUnderwaterComponent, HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var humanoid, out var sprite))
        {
            if (comp.IsUnderwater)
            {
                // Re-applied every frame on purpose: the owning systems re-show their own layers on all
                // sorts of triggers (speaking re-revealed every clothing layer, equipping re-adds them
                // outright), so a one-shot hide loses to them.
                _underwater.Add(uid);
                ApplyUnderwater(uid, humanoid, sprite);
            }
            else if (_underwater.Remove(uid))
            {
                Restore(uid, humanoid, sprite);
            }
        }
    }

    private void OnRemove(Entity<FeroxiUnderwaterComponent> ent, ref ComponentRemove args)
    {
        if (!_underwater.Remove(ent.Owner))
            return;

        // Don't leave a body permanently hidden if the component is stripped mid-swim.
        if (TryComp(ent.Owner, out HumanoidAppearanceComponent? humanoid) &&
            TryComp(ent.Owner, out SpriteComponent? sprite))
        {
            Restore(ent.Owner, humanoid, sprite);
        }
    }

    private void OnTerminating(Entity<FeroxiUnderwaterComponent> ent, ref EntityTerminatingEvent args)
    {
        _underwater.Remove(ent.Owner);
    }

    private void ApplyUnderwater(EntityUid uid, HumanoidAppearanceComponent humanoid, SpriteComponent sprite)
    {
        var dirOffset = GetFinDirOffset(uid);

        // 1. Humanoid base layers - body parts, eyes, snout and so on.
        foreach (var layer in humanoid.BaseLayers.Keys)
        {
            SetLayerVisible(uid, sprite, layer, false);
        }

        // 2. Marking layers, except the fin itself, which is what we're here to show.
        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var markingSprite in prototype.Sprites)
                {
                    if (markingSprite is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerId = $"{marking.MarkingId}-{rsi.RsiState}";

                    if (!FinStates.Contains(rsi.RsiState))
                    {
                        SetLayerVisible(uid, sprite, layerId, false);
                        continue;
                    }

                    SetLayerVisible(uid, sprite, layerId, true);

                    // Set every frame rather than cached: a marking refresh can hand back a new fin
                    // layer that has reverted to DirectionOffset.None.
                    if (_sprite.LayerMapTryGet((uid, sprite), layerId, out var finIndex, false))
                        _sprite.LayerSetDirOffset((uid, sprite), finIndex, dirOffset);
                }
            }
        }

        // 3. Worn clothing. This is the authoritative list of layers the inventory has put on us.
        if (TryComp(uid, out InventorySlotsComponent? slots))
        {
            foreach (var keys in slots.VisualLayerKeys.Values)
            {
                foreach (var key in keys)
                {
                    SetLayerVisible(uid, sprite, key, false);
                }
            }
        }

        // 4. Wound and bleed overlays.
        _woundVisuals.SetWoundVisualsVisible(uid, false);

        // 5. Wetness droplets.
        SetLayerVisible(uid, sprite, WetnessSystem.DropletLayerKey, false);
    }

    private void Restore(EntityUid uid, HumanoidAppearanceComponent humanoid, SpriteComponent sprite)
    {
        // Undo the fin direction offset - the only thing we changed that no other system knows about.
        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var prototype))
                    continue;

                foreach (var markingSprite in prototype.Sprites)
                {
                    if (markingSprite is not SpriteSpecifier.Rsi rsi ||
                        !FinStates.Contains(rsi.RsiState) ||
                        !_sprite.LayerMapTryGet((uid, sprite), $"{marking.MarkingId}-{rsi.RsiState}", out var index, false))
                    {
                        continue;
                    }

                    _sprite.LayerSetDirOffset((uid, sprite), index, DirectionOffset.None);
                }
            }
        }

        // Rebuilds base layers and markings from the humanoid's own state.
        _humanoidAppearance.UpdateSprite((uid, humanoid, sprite));

        // Makes ClientClothingSystem re-render every equipped slot, which removes and re-adds the
        // clothing layers, so their visibility comes back from live inventory state rather than a
        // remembered value.
        if (TryComp(uid, out AppearanceComponent? appearance))
            _appearance.QueueUpdate(uid, appearance);

        // Recomputed from current wound state - a wound healed or a limb lost while underwater comes
        // back correct rather than as whatever was showing when we went under.
        _woundVisuals.SetWoundVisualsVisible(uid, true);

        // The droplet layer exists for exactly as long as this component does, so its presence is the
        // restore condition. If the mob dried off while underwater the layer is already gone.
        if (HasComp<WetVisualsComponent>(uid))
            SetLayerVisible(uid, sprite, WetnessSystem.DropletLayerKey, true);
    }

    private void SetLayerVisible(EntityUid uid, SpriteComponent sprite, Enum key, bool visible)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
            _sprite.LayerSetVisible((uid, sprite), index, visible);
    }

    private void SetLayerVisible(EntityUid uid, SpriteComponent sprite, string key, bool visible)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
            _sprite.LayerSetVisible((uid, sprite), index, visible);
    }

    private DirectionOffset GetFinDirOffset(EntityUid uid)
    {
        // Match the direction the renderer itself picks for this sprite.
        var angle = (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();

        // Every direction except north wants the opposite direction's frame:
        //   east/west - the side fin art reads correctly only when swapped over
        //   south     - its own frame is blank (the body would cover the fin), so it borrows north's
        // DirectionOffset.Flip does all three at once (East<->West, South->North).
        //
        // Deliberately no layer rotation or scale: a negative scale inverts the layer's bounding box
        // (SpriteSystem.Bounds does CenteredAround(offset, size * scale)) and StatusIconOverlay compares
        // icon height against bounds.Height, so an inverted box silently drops every status icon - that
        // was the missing job HUD and novice mark when facing south.
        return angle.GetCardinalDir() == Direction.North
            ? DirectionOffset.None
            : DirectionOffset.Flip;
    }
}
