using Content.Server.Antag.Components;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Antag;

[ByRefEvent]
public record struct PirateAntagAssignmentAttemptEvent(
    EntityUid Rule,
    ICommonSession Session,
    AntagSelectionDefinition Definition,
    bool Cancelled = false,
    string? RejectionReason = null);
