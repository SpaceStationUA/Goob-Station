using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Prototypes;

[Prototype]
public sealed partial class StarTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Shader = default!;
    [DataField(required: true)] public FloatRangeValue SolarMass = default!;
    [DataField(required: true)] public Color Color = default!;
    [DataField(required: true)] public List<StarOrbitSlot> Orbits = default!;
    [DataField(required: true)] public List<ProtoId<AsteroidBeltTypePrototype>> AsteroidBelts = default!;

    /// <summary>Optional fixed name (curated stars like Kyphrus); otherwise a name is generated.</summary>
    [DataField] public string? Name;

    /// <summary>Surface rotation speed for the star shader, radians per second.</summary>
    [DataField] public float Rotation;

    /// <summary>Optional ring system around the star (curated stars like Kyphrus).</summary>
    [DataField] public ProtoId<PlanetaryRingsTypePrototype>? Rings;
}

[DataDefinition]
public sealed partial class StarOrbitSlot
{
    [DataField(required: true)] public OrbitType Type;
    [DataField] public float Prob = 1f;
}
