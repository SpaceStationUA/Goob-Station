using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Pirate.Shared.Vampire.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Vampire;

//Use power events
//public sealed partial class VampireToggleFangsEvent : VampireSelfPowerEvent { }
public sealed partial class VampireOpenMutationsMenu : InstantActionEvent { }
public sealed partial class VampireScreechEvent : VampireSelfPowerEvent { }
public sealed partial class VampirePolymorphEvent : VampireSelfPowerEvent { }
public sealed partial class VampireBloodStealEvent : VampireSelfPowerEvent { }
public sealed partial class VampireUnholyStrengthEvent : VampireSelfPowerEvent { }
public sealed partial class VampireSupernaturalStrengthEvent : VampireSelfPowerEvent { }
public sealed partial class VampireCloakOfDarknessEvent : VampireSelfPowerEvent { }

public sealed partial class VampireGlareEvent : VampireTargetedPowerEvent { }
public sealed partial class VampireHypnotiseEvent : VampireTargetedPowerEvent { }

public sealed partial class VampireSireEvent : VampireTargetedPowerEvent { }
public sealed partial class VampireDarkGiftEvent : VampireTargetedPowerEvent { }


public abstract partial class VampireSelfPowerEvent : InstantActionEvent
{
    [DataField("definitionName")]
    public ProtoId<VampirePowerProtype> DefinitionName = default!;
};
public abstract partial class VampireTargetedPowerEvent : EntityTargetActionEvent
{
    [DataField("definitionName")]
    public ProtoId<VampirePowerProtype> DefinitionName = default!;
};
public sealed partial class VampirePassiveActionEvent : BaseActionEvent
{
    [DataField("definitionName")]
    public ProtoId<VampirePowerProtype> DefinitionName = default!;
};

//Purchase passive events
[Serializable, NetSerializable]
public sealed partial class VampirePurchaseUnnaturalStrength : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed partial class VampireBloodChangedEvent : EntityEventArgs { }

//Doafter events
[Serializable, NetSerializable]
public sealed partial class VampireDrinkBloodDoAfterEvent : DoAfterEvent
{
    [DataField]
    public float Volume = 0;

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class VampireHypnotiseDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TimeSpan? Duration = TimeSpan.Zero;

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class VampireSireDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class VampireDarkGiftDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
