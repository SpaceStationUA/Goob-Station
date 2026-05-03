using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Species.Systems;

[Serializable, NetSerializable]
public sealed partial class CoolOffDoAfterEvent : SimpleDoAfterEvent;