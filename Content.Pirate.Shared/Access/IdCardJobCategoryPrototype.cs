using Content.Shared.Roles;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Access;

/// <summary>
/// Groups ID-console job presets into selectable categories.
/// </summary>
[Prototype]
public sealed partial class IdCardJobCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs { get; private set; } = new();

}
