using Content.Shared.Alert;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.TerrorSpider;

[RegisterComponent]
public sealed partial class StealthOnWebComponent : Component
{
    public readonly HashSet<SpiderWebContact> Contacts = new();
}

public readonly record struct SpiderWebContact(EntityUid Other, string OurFixtureId, string OtherFixtureId);

[RegisterComponent]
public sealed partial class EggHolderComponent : Component
{
    [DataField]
    public int Counter;
}

[RegisterComponent]
public sealed partial class HasEggHolderComponent : Component;

[RegisterComponent]
public sealed partial class TerrorPrincessComponent : Component
{
    [DataField]
    public LocId Briefing = "terror-spider-princess-briefing";

    [DataField]
    public List<EntProtoId> Eggs = new()
    {
        "TerrorRedEggSpiderFertilized",
        "TerrorGreenSpiderFertilized",
        "TerrorGrayEggSpiderFertilized"
    };

    [DataField]
    public EntProtoId LayEggActionId = "ActionEggsLaying";

    [DataField]
    public EntityUid? LayEggAction;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class TerrorSpiderComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class TerrorSpiderWebOccluderComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpawnOnActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "Spawn";

    [DataField(required: true)]
    public EntProtoId EntityToSpawn;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class WrapEntityHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Hold;

    [DataField]
    public TimeSpan UnWrapItemTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan UnWrapHandTime = TimeSpan.FromSeconds(30);

    [DataField]
    public string ContainerId = "entity";

    public BaseContainer? Container;

    [DataField]
    public ProtoId<AlertPrototype> WrappedAlert = "Wrapped";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class WrappedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Holder;

    [DataField]
    public ProtoId<AlertPrototype> WrappedAlert = "Wrapped";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class EntityBeaconComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Range;

    [DataField]
    public int RangeLimit = 40;

    [DataField(required: true)]
    public List<EntProtoId> EntitiesToSpawn = [];

    public HashSet<EntityCoordinates> CoordinatesToSpawn = [];

    [DataField("nextUpdate", customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);
}
