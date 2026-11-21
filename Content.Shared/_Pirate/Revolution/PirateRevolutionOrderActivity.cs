// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Revolution;

[Serializable, NetSerializable]
public enum RevOrderSource : byte
{
    Automatic,
    Admin,
    ServerConsole,
}

[Serializable, NetSerializable]
public sealed record RevOrderLogEntry(TimeSpan RoundTime, RevOrderSource Source, string? Admin,
    string Order, string Announcement);

[Serializable, NetSerializable]
public sealed class RevOrderActivityState(int round, bool automatic, bool ruleActive, int page, int lastPage,
    List<RevOrderLogEntry> entries) : EuiMessageBase
{
    public readonly int Round = round;
    public readonly bool Automatic = automatic;
    public readonly bool RuleActive = ruleActive;
    public readonly int Page = page;
    public readonly int LastPage = lastPage;
    public readonly List<RevOrderLogEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class RevOrderReadHistory(int page) : EuiMessageBase
{
    public readonly int Page = page;
}

[Serializable, NetSerializable]
public sealed class RevOrderSetAutomatic(int round, bool enabled) : EuiMessageBase
{
    public readonly int Round = round;
    public readonly bool Enabled = enabled;
}
