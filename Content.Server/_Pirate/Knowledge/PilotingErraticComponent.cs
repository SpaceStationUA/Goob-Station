// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.Knowledge;

[RegisterComponent, Access(typeof(PilotingSkillSystem))]
public sealed partial class PilotingErraticComponent : Component
{
    [ViewVariables]
    public float DriftSign = 1f;

    [ViewVariables]
    public TimeSpan NextDriftChange;

    [ViewVariables]
    public TimeSpan NextRoll;

    [ViewVariables]
    public TimeSpan DroppedUntil;

    [ViewVariables]
    // Active control confusion flags.
    public int ConfusedControls;

    [ViewVariables]
    public TimeSpan ConfusedUntil;

    [ViewVariables]
    public float DiagonalSign = 1f;

    [ViewVariables]
    public float SurgeFactor = 1f;

    [ViewVariables]
    public TimeSpan NextSurgeChange;
}
