using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class PlanetTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public List<OrbitType> Orbit = default!;
    [DataField(required: true)] public float AtmosphereProbability = default!;
    [DataField] public List<ProtoId<PlanetaryAtmosphereTypePrototype>> Atmospheres = new();
    [DataField(required: true)] public float LiquidProbability = default!;
    [DataField] public List<ProtoId<PlanetaryLiquidTypePrototype>> Liquids = new();
    // ShaderPrototype is client-only, so this stays a plain id string (validated by the client).
    [DataField(required: true)] public string Shader = default!;

    /// <summary>
    /// Whether this planet type can host a landable surface (descent z-stack). Gas and ice
    /// giants have no surface to land on.
    /// </summary>
    [DataField] public bool Landable = true;

    [DataField(required: true)] public float RingProbability = default!;
    [DataField(required: true)] public FloatRangeValue EarthMass = default!;
    [DataField(required: true)] public List<ProtoId<PlanetPalettePrototype>> Palettes = default!;
    [DataField(required: true)] public List<ProtoId<PlanetaryRingsTypePrototype>> Rings = default!;
    [DataField] public PlanetCustomValueRanges CustomData = new PlanetCustomValueRanges();
    [DataField] public int BasePrettiness = -100;
}

public enum OrbitType
{
    InnerHot,
    InnerHabitable,
    InnerCold,
    Belt,
    OuterWarm,
    OuterCold
}

[DataDefinition]
public sealed partial class PlanetCustomValueRanges
{
    [DataField] public Dictionary<string, FloatRangeValue> Floats = new Dictionary<string, FloatRangeValue>();
    [DataField] public Dictionary<string, IntRangeValue> Ints = new Dictionary<string, IntRangeValue>();
    [DataField] public Dictionary<string, ColorRangeValue> Colors = new Dictionary<string, ColorRangeValue>();
}
