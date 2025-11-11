using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Content.Shared.Customization.Systems;
using Content.Shared.Roles;
using Content.Shared.Traits;


namespace Content.Shared._Pirate.Contractors.Prototypes;

/// <summary>
/// Prototype representing a character's Lifepath in YAML.
/// </summary>
[Prototype("lifepath")]
public sealed partial class LifepathPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; } = string.Empty;

    [DataField]
    public string NameKey { get; } = string.Empty;

    [DataField]
    public string DescriptionKey { get; } = string.Empty;

    [DataField]
    public List<JobRequirement> Requirements = new();
}