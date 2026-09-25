// SPDX-License-Identifier: MIT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration.BanList;

[Serializable, NetSerializable]
public sealed class BanListEuiState : EuiStateBase
{
    public BanListEuiState(string banListPlayerName, List<SharedBan> bans, List<SharedBan> roleBans, List<SharedBan> chatBans) // Pirate: chat ban data
    {
        BanListPlayerName = banListPlayerName;
        Bans = bans;
        RoleBans = roleBans;
        ChatBans = chatBans; // Pirate: chat ban data
    }

    public string BanListPlayerName { get; }
    public List<SharedBan> Bans { get; }
    public List<SharedBan> RoleBans { get; }
    public List<SharedBan> ChatBans { get; } // Pirate: chat ban data
}

#region Pirate: chat ban data
[Serializable, NetSerializable]
public sealed class PardonChatBanRequest(int banId) : EuiMessageBase
{
    public int BanId { get; } = banId;
}
#endregion Pirate: chat ban data
