using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.SecretTP.Components;

[RegisterComponent]
public sealed partial class SecretTPComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, int> JobPoints = new();

    [DataField]
    public Dictionary<ProtoId<AntagPrototype>, int> AntagPoints = new();

    [DataField]
    public List<ProtoId<EntityPrototype>> RuleBlacklist = new();

    [DataField]
    public Dictionary<ProtoId<EntityPrototype>, Dictionary<ProtoId<DepartmentPrototype>, int>> RuleMinimumAliveDepartments = new();

    [DataField]
    public float GreenShiftWeight = 4f;

    [DataField]
    public float RedShiftWeight = 6f;

    [DataField]
    public float DeathReleaseSeconds = 900f;

    [ViewVariables]
    public int TotalPoints;

    [ViewVariables]
    public int ReservedPoints;

    [ViewVariables]
    public Dictionary<EntityUid, int> Reservations = new();

    [ViewVariables]
    public Dictionary<string, Queue<int>> PendingRuleReservations = new();
}
