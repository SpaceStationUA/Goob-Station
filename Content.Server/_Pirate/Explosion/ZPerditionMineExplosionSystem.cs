// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Components;
using Content.Shared.Explosion;

namespace Content.Server._Pirate.Explosion;

/// <summary>
/// Keeps ZPerdition's anti-personnel mines from damaging the station's airtight structures.
/// Other damage sources and explosion types are unaffected.
/// </summary>
public sealed class ZPerditionMineExplosionSystem : EntitySystem
{
    private const string ExplosionPrototype = "ZPerditionLandMine";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirtightComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
    }

    private static void OnGetExplosionResistance(Entity<AirtightComponent> _, ref GetExplosionResistanceEvent args)
    {
        if (args.ExplosionPrototype == ExplosionPrototype)
            args.DamageCoefficient = 0;
    }
}
