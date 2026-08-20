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

/// <summary>Shows only a submerged Feroxi's dorsal fin.</summary>
/// <remarks>Only hides layers that their owning systems can rebuild from live state.</remarks>
public sealed class FeroxiUnderwaterVisualsSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly WoundableVisualsSystem _woundVisuals = default!;

    /// <summary>Dorsal-fin states that remain visible while submerged.</summary>
    private static readonly HashSet<string> FinStates =
    [
        "feroxi-dorsal",
        "feroxi-dorsal-tip",
        "feroxi-dorsal-stripes",
        "feroxi-tail-second-dorsal-tip",
    ];

    private readonly HashSet<EntityUid> _underwater = new();

    public override void Initialize()
    {
        base.Initialize();

        // ComponentShutdown already has a subscriber, so use removal events for cleanup.
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
                // Appearance updates can re-show layers.
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

        foreach (var layer in humanoid.BaseLayers.Keys)
        {
            SetLayerVisible(uid, sprite, layer, false);
        }

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

                    // Marking refreshes reset the direction offset.
                    if (_sprite.LayerMapTryGet((uid, sprite), layerId, out var finIndex, false))
                        _sprite.LayerSetDirOffset((uid, sprite), finIndex, dirOffset);
                }
            }
        }

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

        _woundVisuals.SetWoundVisualsVisible(uid, false);

        SetLayerVisible(uid, sprite, WetnessSystem.DropletLayerKey, false);
    }

    private void Restore(EntityUid uid, HumanoidAppearanceComponent humanoid, SpriteComponent sprite)
    {
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

        _humanoidAppearance.UpdateSprite((uid, humanoid, sprite));

        // Re-render worn clothing.
        if (TryComp(uid, out AppearanceComponent? appearance))
            _appearance.QueueUpdate(uid, appearance);

        _woundVisuals.SetWoundVisualsVisible(uid, true);

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
        var angle = (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();

        // Flip swaps east/west and uses north for south; scale would hide status icons.
        return angle.GetCardinalDir() == Direction.North
            ? DirectionOffset.None
            : DirectionOffset.Flip;
    }
}
