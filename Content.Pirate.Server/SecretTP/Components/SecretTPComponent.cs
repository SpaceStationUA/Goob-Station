namespace Content.Pirate.Server.SecretTP.Components;

[RegisterComponent]
public sealed partial class SecretTPComponent : Component
{
    [DataField]
    public Dictionary<string, int> JobPoints = new();

    [DataField]
    public Dictionary<string, int> AntagPoints = new();

    [DataField]
    public List<string> RuleBlacklist = new();

    [DataField]
    public Dictionary<string, Dictionary<string, int>> RuleMinimumAliveDepartments = new();

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
