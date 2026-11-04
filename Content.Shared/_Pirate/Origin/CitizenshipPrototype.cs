using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Origin;

/// <summary>
/// Prototype representing a character's Citizenship in YAML.
/// </summary>
[Prototype("citizenship")]
public sealed partial class CitizenshipPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string NameKey { get; private set; } = string.Empty;

    [DataField]
    public List<JobRequirement> Requirements = new();

    [DataField]
    public ProtoId<EntityPrototype> PassportPrototype { get; private set; } = new();

    [DataField("languages")]
    public List<string> Languages { get; private set; } = new();
}
