using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.Psionics.Events.PowerDoAfterEvents;

/// <summary>
/// DoAfter event for the psionic sacrifice ritual.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SacrificeDoAfterEvent : SimpleDoAfterEvent;
