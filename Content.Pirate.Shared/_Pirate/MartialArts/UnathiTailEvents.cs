using Robust.Shared.Serialization;

namespace Content.Pirate.Shared._Pirate.MartialArts;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class UnathiTailTripPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class UnathiTailChokePerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class UnathiTailThrowPerformedEvent : EntityEventArgs;
