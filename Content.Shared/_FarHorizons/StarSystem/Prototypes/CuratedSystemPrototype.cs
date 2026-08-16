using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

/// <summary>
/// Far Horizons: a hand-authored planet with fixed values (no rolled ranges) — the named
/// worlds of curated star systems (Fervidus, Merak, Asclepiu, Aerumna, Thrascias).
/// </summary>
[Prototype("curatedPlanet")]
public sealed partial class CuratedPlanetPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Name = default!;
    [DataField(required: true)] public string Shader = default!;
    [DataField(required: true)] public float EarthMass;
    [DataField] public float Rotation;
    [DataField(required: true)] public ProtoId<PlanetPalettePrototype> Palette;
    [DataField] public float HueShift;
    [DataField] public float SaturationShift;
    [DataField] public ProtoId<PlanetaryAtmosphereTypePrototype>? Atmosphere;
    [DataField] public ProtoId<PlanetaryLiquidTypePrototype>? Liquid;
    [DataField] public ProtoId<PlanetaryRingsTypePrototype>? Rings;
    [DataField] public int BasePrettiness = -100;
    [DataField] public bool Landable = true;
    [DataField] public Dictionary<string, float> CustomFloats = new();
}

/// <summary>
/// Far Horizons: a curated star system — a star type plus named planets at fixed distances
/// and angles. Blended with the procedural generation for the home system.
/// </summary>
[Prototype("curatedSystem")]
public sealed partial class CuratedSystemPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public ProtoId<StarTypePrototype> Star = default!;
    [DataField] public List<CuratedSystemPlanet> Planets = new();
}

[DataDefinition]
public sealed partial class CuratedSystemPlanet
{
    [DataField(required: true)] public ProtoId<CuratedPlanetPrototype> Planet = default!;
    [DataField(required: true)] public float Distance;
    [DataField] public float Angle;
}
