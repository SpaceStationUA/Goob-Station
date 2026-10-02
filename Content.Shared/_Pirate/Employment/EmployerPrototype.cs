using Content.Shared.Roles;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Employment;

/// <summary>
/// Prototype representing a character's employer in YAML.
/// </summary>
[Prototype("employer")]
public sealed partial class EmployerPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string NameKey { get; private set; } = string.Empty;

    [DataField]
    public string DescriptionKey { get; private set; } = string.Empty;

    [DataField]
    public string EmployeeRelationsKey { get; private set; } = string.Empty;

    [DataField]
    public List<string> RoleplayRecommendations { get; private set; } = new();

    [DataField]
    public ResPath LogoPath { get; private set; } = new("/Textures/_Pirate/Interface/Employers/unaffiliated.png");

    [DataField]
    public Color? AccentColor { get; private set; }

    [DataField]
    public int Priority { get; private set; }

    [DataField]
    public List<string> Departments { get; private set; } = new();

    [DataField]
    public List<JobRequirement> Requirements = new();

}
