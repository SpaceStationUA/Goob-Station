// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;

namespace Content.Server.Body.Systems;

public sealed partial class ThermalRegulatorSystem
{
    /// <summary>Pirate: keep protected thermal parameters owned by the regulator system.</summary>
    public void SetHeatRegulation(Entity<ThermalRegulatorComponent?> ent,
        float shivering, float sweating, float metabolism, float regulation)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.ShiveringHeatRegulation = shivering;
        ent.Comp.SweatHeatRegulation = sweating;
        ent.Comp.MetabolismHeat = metabolism;
        ent.Comp.ImplicitHeatRegulation = regulation;
    }
}
