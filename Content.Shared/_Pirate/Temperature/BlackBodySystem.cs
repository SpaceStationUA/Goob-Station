// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;

namespace Content.Shared._Pirate.Temperature;

public sealed class BlackBodySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackBodyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlackBodyComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
    }

    private void OnStartup(Entity<BlackBodyComponent> ent, ref ComponentStartup args)
    {
        var appearance = EnsureComp<AppearanceComponent>(ent);
        if (TryComp(ent, out TemperatureComponent? temperature))
        {
            _appearance.SetData(
                ent.Owner,
                BlackBodyVisuals.Temperature,
                temperature.CurrentTemperature,
                appearance);
        }
    }

    private void OnTemperatureChanged(Entity<BlackBodyComponent> ent, ref OnTemperatureChangeEvent args)
    {
        _appearance.SetData(ent.Owner, BlackBodyVisuals.Temperature, args.CurrentTemperature);
    }
}
