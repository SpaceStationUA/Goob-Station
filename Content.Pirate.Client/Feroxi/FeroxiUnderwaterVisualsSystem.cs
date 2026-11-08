using Content.Client._Shitmed.Medical.Surgery.Wounds;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Pirate.Client.Wetness;
using Content.Pirate.Shared.Feroxi;
using Content.Pirate.Shared.Wetness.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Feroxi;

/// <remarks>Only hides layers that their owning systems can rebuild from live state.</remarks>
public sealed class FeroxiUnderwaterVisualsSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly WoundableVisualsSystem _woundVisuals = default!;

    private static readonly ResPath FeroxiFins = new("/Textures/_Pirate/Mobs/Customization/Feroxi/submerged_fins.rsi");
    private static readonly ResPath PortedFins = new("/Textures/_Pirate/Mobs/Customization/Feroxi/submerged_ported_fins.rsi");

    // Target state names must differ from keys to detect swapped layers.
    private static readonly Dictionary<string, SpriteSpecifier.Rsi> SubmergedFins = new()
    {
        // The tip layer recolours the same pixels as the tail fin.
        ["feroxi-dorsal"] = new(FeroxiFins, "dorsal"),
        ["feroxi-dorsal-tip"] = new(FeroxiFins, "dorsal-tip"),
        ["feroxi-dorsal-stripes"] = new(FeroxiFins, "dorsal-stripes"),
        ["feroxi-tail"] = new(FeroxiFins, "second-dorsal"),
        ["feroxi-tail-second-dorsal-tip"] = new(FeroxiFins, "second-dorsal"),

        // The BEHIND layer draws the whole fin for split tail layers.
        ["m_tail_orca_BEHIND_primary"] = new(PortedFins, "orca"),
        ["m_tail_datashark_BEHIND_C"] = new(PortedFins, "datashark"),
        ["m_tailwag_datashark_BEHIND_C"] = new(PortedFins, "datashark"),
        ["data_shark_wag_primary_BEHIND"] = new(PortedFins, "datashark-wag"),
        ["fish_primary_BEHIND"] = new(PortedFins, "fish"),
        ["fish_wag_primary_BEHIND"] = new(PortedFins, "fish"),
    };

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
                    if (markingSprite is not SpriteSpecifier.Rsi rsi ||
                        !_sprite.LayerMapTryGet((uid, sprite), $"{marking.MarkingId}-{rsi.RsiState}", out var index, false))
                    {
                        continue;
                    }

                    if (!SubmergedFins.TryGetValue(rsi.RsiState, out var fin))
                    {
                        _sprite.LayerSetVisible((uid, sprite), index, false);
                        continue;
                    }

                    if (_sprite.LayerGetRsiState((uid, sprite), index).Name != fin.RsiState)
                        _sprite.LayerSetSprite((uid, sprite), index, fin);

                    // Clothing that hides the tail on land is hidden underwater.
                    _sprite.LayerSetVisible((uid, sprite), index, true);
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
}
