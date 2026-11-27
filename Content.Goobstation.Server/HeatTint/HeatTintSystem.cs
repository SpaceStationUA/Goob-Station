using System.Numerics;
using Content.Goobstation.Shared.HeatTint;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;

namespace Content.Goobstation.Server.HeatTint;

public sealed partial class HeatTintSystem : SharedHeatTintSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeatTintComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HeatTintComponent, OnTemperatureChangeEvent>(OnTemperatureChange);
        SubscribeLocalEvent<HeatTintComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnMapInit(Entity<HeatTintComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<TemperatureComponent>(ent, out var temp))
            SetTintTemperature(ent, temp.CurrentTemperature);
    }

    private void OnTemperatureChange(Entity<HeatTintComponent> ent, ref OnTemperatureChangeEvent args)
    {
        SetTintTemperature(ent, args.CurrentTemperature);
    }

    private void OnSolutionChanged(Entity<HeatTintComponent> ent, ref SolutionContainerChangedEvent args)
    {
        SetTintTemperature(ent, args.Solution.Temperature);
    }

    private void SetTintTemperature(Entity<HeatTintComponent> ent, float temperature)
    {
        if (ent.Comp.FilterTintUpdates &&
            _appearance.TryGetData<float>(ent, HeatTintVisuals.Temperature, out var lastTemperature) &&
            (lastTemperature == temperature || TintBucket(ent.Comp, lastTemperature) == TintBucket(ent.Comp, temperature)))
            return;

        _appearance.SetData(ent, HeatTintVisuals.Temperature, temperature);
    }

    private static Vector4 TintBucket(HeatTintComponent heat, float temperature)
    {
        // Pirate: keep 64 subdivisions per 8-bit channel step for tinting textured, lit sprites.
        var scaled = GetHeatColor(heat.ColorGradient, temperature).RGBA * (255 * 64);
        return Vector4.Round(scaled, MidpointRounding.ToNegativeInfinity);
    }
}
