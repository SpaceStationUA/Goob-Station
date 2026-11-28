// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Supermatter;

/// <summary>Only a full-size supermatter engine can sustain a resonance cascade.</summary>
[RegisterComponent]
public sealed partial class CascadeCoreComponent : Component
{
    [DataField]
    public bool Destabilized;
}

[RegisterComponent]
public sealed partial class SupermatterDestabilizerComponent : Component;

[Serializable, NetSerializable]
public sealed partial class DestabilizeCrystalDoAfterEvent : SimpleDoAfterEvent;

public sealed class SupermatterCascadeEvent(EntityUid core) : EntityEventArgs
{
    public readonly EntityUid Core = core;
}
