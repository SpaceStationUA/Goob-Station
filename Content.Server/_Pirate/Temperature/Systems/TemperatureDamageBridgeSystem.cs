// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Temperature.Components;
using Content.Shared.Temperature.Components;

namespace Content.Server._Pirate.Temperature.Systems;

/// <summary>
/// Pirate - adapts Starlight TemperatureDamage prototypes to the local TemperatureComponent-based system.
/// </summary>
public sealed class TemperatureDamageBridgeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemperatureDamageComponent, ComponentStartup>(OnTemperatureDamageStartup);
        SubscribeLocalEvent<TemperatureComponent, ComponentStartup>(OnTemperatureStartup);
    }

    private void OnTemperatureDamageStartup(EntityUid uid, TemperatureDamageComponent component, ComponentStartup args)
    {
        Apply(uid, component);
    }

    private void OnTemperatureStartup(EntityUid uid, TemperatureComponent component, ComponentStartup args)
    {
        if (TryComp<TemperatureDamageComponent>(uid, out var temperatureDamage))
            Apply(temperatureDamage, component);
    }

    private void Apply(EntityUid uid, TemperatureDamageComponent source)
    {
        if (!TryComp<TemperatureComponent>(uid, out var temperature))
            return;

        Apply(source, temperature);
    }

    private void Apply(TemperatureDamageComponent source, TemperatureComponent temperature)
    {
        temperature.HeatDamageThreshold = source.HeatDamageThreshold;
        temperature.ColdDamageThreshold = source.ColdDamageThreshold;
        temperature.ParentHeatDamageThreshold = source.ParentHeatDamageThreshold;
        temperature.ParentColdDamageThreshold = source.ParentColdDamageThreshold;
        temperature.ColdDamage = source.ColdDamage;
        temperature.HeatDamage = source.HeatDamage;
        temperature.DamageCap = source.DamageCap;
        temperature.TakingDamage = source.TakingDamage;
        temperature.HotAlert = source.HotAlert;
        temperature.ColdAlert = source.ColdAlert;
    }
}
