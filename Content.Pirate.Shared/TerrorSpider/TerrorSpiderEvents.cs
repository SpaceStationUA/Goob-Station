using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.TerrorSpider;

public sealed partial class EggInjectionEvent : EntityTargetActionEvent
{
    [DataField]
    public int InjectionDelay = 6;
}

[Serializable, NetSerializable]
public sealed partial class EggInjectionDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class EggsLayingEvent : InstantActionEvent;

public sealed class EggsInjectedEvent : EntityEventArgs;

public sealed partial class AcidVentEvent : EntityTargetActionEvent;

public sealed partial class EvolveEvent : InstantActionEvent;

public sealed partial class SpiderWebBuildingActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Building;
}

public sealed partial class WrapActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan WrapTime = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId WrapContainerId = "EffectTerrorCocoon";
}

[Serializable, NetSerializable]
public sealed partial class WrapDoAfterEvent : DoAfterEvent
{
    public EntProtoId WrapContainerId;

    public WrapDoAfterEvent(EntProtoId wrapContainerId)
    {
        WrapContainerId = wrapContainerId;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class UnwrapDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class SpawnOnActionEvent : InstantActionEvent;

public sealed partial class EMPScreamEvent : InstantActionEvent
{
    [DataField]
    public float Power = 2.5f;

    [DataField]
    public TimeSpan DurationMultiply = TimeSpan.FromSeconds(2);

    [DataField]
    public float EnergyConsumption = 5000f;

    [DataField]
    public SoundSpecifier? ScreamSound = new SoundPathSpecifier("/Audio/Effects/changeling_shriek.ogg");
}

public sealed partial class UnWrapAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public enum EggsLayingUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class EggsLayingBuiMsg : BoundUserInterfaceMessage
{
    public EntProtoId Egg { get; set; }
}

[Serializable, NetSerializable]
public sealed class EggsLayingBuiState : BoundUserInterfaceState;
