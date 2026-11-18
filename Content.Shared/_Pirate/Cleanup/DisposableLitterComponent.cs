// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Cleanup;

/// <summary>Eligible for cleanup when produced by use, eating or unwrapping.</summary>
[RegisterComponent]
public sealed partial class DisposableLitterComponent : Component;

[ByRefEvent]
public readonly record struct LitterProducedEvent;

[ByRefEvent]
public readonly record struct CartridgeSpentChangedEvent;

public static class RoundLitterExtensions
{
    /// <summary>Marks eligible outputs of a gameplay action. Direct entity spawns never call this.</summary>
    public static void NotifyLitterProduced(this IEntityManager entities, EntityUid uid)
    {
        var ev = new LitterProducedEvent();
        entities.EventBus.RaiseLocalEvent(uid, ref ev);
    }
}
