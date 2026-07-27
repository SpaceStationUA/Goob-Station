using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.PAI;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PAIMedInjectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxCapacity = 15f;

    [DataField, AutoNetworkedField]
    public float CurrentCapacity = 15f;

    [DataField]
    public float RechargeAmount = 1f;

    [DataField]
    public float RechargeTime = 12f;

    [DataField]
    public float MedCooldown = 30f;

    [DataField, AutoNetworkedField]
    public Dictionary<string, TimeSpan> LastUsed = new();

    public TimeSpan NextRecharge = TimeSpan.Zero;
}

[Serializable, NetSerializable]
public sealed class PAIMedInjectorBoundUserInterfaceState : BoundUserInterfaceState
{
    public float CurrentCapacity { get; }
    public float MaxCapacity { get; }
    public bool CarrierPresent { get; }
    public List<MedButtonState> Meds { get; }

    public PAIMedInjectorBoundUserInterfaceState(
        float currentCapacity,
        float maxCapacity,
        bool carrierPresent,
        List<MedButtonState> meds)
    {
        CurrentCapacity = currentCapacity;
        MaxCapacity = maxCapacity;
        CarrierPresent = carrierPresent;
        Meds = meds;
    }
}

[Serializable, NetSerializable]
public sealed class MedButtonState
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public float Cost { get; }
    public bool Available { get; set; }
    public float CooldownRemaining { get; set; }

    public MedButtonState(string id, string name, string description, float cost)
    {
        Id = id;
        Name = name;
        Description = description;
        Cost = cost;
    }
}

[Serializable, NetSerializable]
public sealed class PAIMedInjectorInjectMessage : BoundUserInterfaceMessage
{
    public string MedId { get; }

    public PAIMedInjectorInjectMessage(string medId)
    {
        MedId = medId;
    }
}

[Serializable, NetSerializable]
public enum PAIMedInjectorUiKey : byte
{
    Key
}
