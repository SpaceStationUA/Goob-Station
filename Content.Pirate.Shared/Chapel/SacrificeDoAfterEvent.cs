using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Pirate.Shared.Chapel;

[Serializable, NetSerializable]
public sealed partial class SacrificeDoAfterEvent : SimpleDoAfterEvent { }
