// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Grab;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Common.MartialArts;

[RegisterComponent]
public sealed partial class MartialArtBlockedComponent : Component
{
    [DataField]
    public MartialArtsForms Form;

    [DataField]
    public bool StaminaDmgBlock = false;
}
public abstract partial class GrabStagesOverrideComponent : Component
{
    public readonly GrabStage StartingStage = GrabStage.Soft;
}

[RegisterComponent]
[NetworkedComponent]
//[AutoGenerateComponentState] //Original
[AutoGenerateComponentState(raiseAfterAutoHandleState: true)] //Pirate Changes
public sealed partial class MartialArtsKnowledgeComponent : GrabStagesOverrideComponent
{
    [DataField]
    [AutoNetworkedField]
    public MartialArtsForms MartialArtsForm = MartialArtsForms.CloseQuartersCombat;

    [DataField]
    [AutoNetworkedField]
    public bool Blocked;

    [DataField] // Pirate: preserve all original damage types when switching martial arts
    [AutoNetworkedField]
    public Dictionary<string, float> OriginalFistDamage = new();

    // Pirate start - martial art switching
    [DataField]
    public MartialArtsForms? PendingForm;

    [DataField]
    public TimeSpan PendingFormExpiry;

    // Pirate end
}

public enum MartialArtsForms
{
    CorporateJudo,
    CloseQuartersCombat,
    SleepingCarp,
    Capoeira,
    KungFuDragon,
    Ninjutsu,
    HellRip,
    // Pirate martial arts start
    LegendaryCloseQuartersCombat,
    UnathiTailArts,
    // Pirate end
}
