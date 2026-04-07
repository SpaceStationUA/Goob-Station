using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared._JustDecor.MartialArts.Events;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCTakedownPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCDisarmPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCThrowPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCChokePerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCChainPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCCounterPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCInterrogationPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCStealthTakedownPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCRushPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class LegendaryCQCFinisherPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed partial class LegendaryCQCInterrogationDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class LegendaryCQCChokeDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
