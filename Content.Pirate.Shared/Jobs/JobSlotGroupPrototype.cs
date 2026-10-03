using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Pirate.Jobs;

/// <summary>
/// Jobs that draw from one shared station slot pool.
/// </summary>
[Prototype]
public sealed partial class JobSlotGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs { get; private set; } = new();
}
