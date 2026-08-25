namespace Content.Server._Pirate.GameTicking;

[ByRefEvent]
public record struct PirateGameRuleAddAttemptEvent(
    string RuleId,
    bool Cancelled = false,
    string? RejectionReason = null);
