// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Pirate.Shared.AlertLevel;

[RegisterComponent]
public sealed partial class AlertLevelRestrictedComponent : Component
{
    [DataField]
    public AlertLevelRequirement Requirement = new();

    [DataField]
    public SoundSpecifier? DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    [DataField]
    public LocId DenyMessage = "alert-level-restricted-denied";

    [DataField]
    public LocId ShutdownMessage = "alert-level-restricted-shutdown";

    [DataField]
    public LocId? ExamineMessage = "alert-level-restricted-examine";
}

[ByRefEvent]
public record struct AlertLevelRestrictionRevokedEvent
{
    public bool Disabled;
}
