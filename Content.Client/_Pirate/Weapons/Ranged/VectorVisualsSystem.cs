// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Weapons.Ranged;

public sealed partial class VectorVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VectorComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<VectorComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var state = "vector";
        var stockExtended = args.AppearanceData.TryGetValue(VectorVisuals.StockFolded, out var folded) && folded is false;
        if (stockExtended)
            state += "_stock";

        var loaded = args.AppearanceData.TryGetValue(AmmoVisuals.MagLoaded, out var mag) && mag is true;
        if (loaded)
        {
            var extended = args.AppearanceData.TryGetValue(AmmoVisuals.AmmoMax, out var capacity) &&
                           capacity is int count && count >= 30;
            state += extended ? "_longmag" : "_mag";
        }

        if (!loaded ||
            !args.AppearanceData.TryGetValue(AmmoVisuals.HasAmmo, out var ammo) || ammo is false ||
            args.AppearanceData.TryGetValue(AmmoVisuals.BoltClosed, out var bolt) && bolt is false)
            state += "_slide";

        _sprite.LayerSetRsiState((ent, args.Sprite), VectorVisuals.Base, state);
        // Folded sprites are centered nine pixels left of the extended sprites.
        _sprite.LayerSetOffset((ent, args.Sprite), VectorVisuals.Base,
            stockExtended ? Vector2.Zero : new Vector2(9f / 32f, 0f));
    }
}
