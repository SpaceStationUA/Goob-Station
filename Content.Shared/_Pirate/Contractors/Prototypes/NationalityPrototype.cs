using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Content.Shared.Customization.Systems;
using Content.Shared.Roles;
using Content.Shared.Traits;


namespace Content.Shared._Pirate.Contractors.Prototypes;

/// <summary>
/// Prototype representing a character's nationality in YAML.
/// </summary>
[Prototype("nationality")]
public sealed partial class NationalityPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; } = string.Empty;

    [DataField]
    public string NameKey { get; } = string.Empty;

    [DataField]
    public string DescriptionKey { get; } = string.Empty;

    [DataField, ViewVariables]
    public HashSet<ProtoId<NationalityPrototype>> Allied { get; } = new();

    [DataField, ViewVariables]
    public HashSet<ProtoId<NationalityPrototype>> Hostile { get; } = new();

    [DataField]
    public List<JobRequirement> Requirements = new();
    
    [DataField]
    public ProtoId<EntityPrototype> PassportPrototype { get; } = new();

    [DataField("languages")]
    public List<string> Languages { get; private set; } = new();
}