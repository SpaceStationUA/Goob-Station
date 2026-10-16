// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Utility;

namespace Content.Client._Pirate.StationRecords;

public static class RecordStatusReason
{
    public static FormattedMessage Format(string console, string status, string reason)
    {
        var title = Loc.TryGetString($"records-status-reason-{console}-{status.ToLowerInvariant()}", out var specific)
            ? specific
            : Loc.GetString("records-status-reason");

        var message = new FormattedMessage();
        message.PushColor(Color.DarkGray);
        message.AddText($"{title}:");
        message.Pop();
        message.AddText($" {reason}");
        return message;
    }
}
