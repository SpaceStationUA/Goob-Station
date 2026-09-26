// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Administration.BanList;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Server.Administration.BanList;

public sealed class BanListEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IBanManager _banManager = default!; // Pirate: chat ban list

    public BanListEui()
    {
        IoCManager.InjectDependencies(this);
    }

    private Guid BanListPlayer { get; set; }
    private string BanListPlayerName { get; set; } = string.Empty;
    private List<SharedBan> Bans { get; } = new();
    private List<SharedBan> RoleBans { get; } = new();
    private List<SharedBan> ChatBans { get; } = new(); // Pirate: chat ban list

    public override void Opened()
    {
        base.Opened();

        _admins.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();

        _admins.OnPermsChanged -= OnPermsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        return new BanListEuiState(BanListPlayerName, Bans, RoleBans, ChatBans); // Pirate: chat ban list
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Ban))
        {
            Close();
        }
    }

    private async Task LoadBans(NetUserId userId)
    {
        await LoadBansCore(userId, BanType.Server, Bans);
        await LoadBansCore(userId, BanType.Role, RoleBans);
        await LoadBansCore(userId, BanType.OOC, ChatBans); // Pirate: chat ban list
        await LoadBansCore(userId, BanType.LOOC, ChatBans); // Pirate: chat ban list
        await LoadBansCore(userId, BanType.Deadchat, ChatBans); // Pirate: chat ban list
    }

    private async Task LoadBansCore(NetUserId userId, BanType banType, List<SharedBan> list)
    {
        foreach (var ban in await _db.GetBansAsync(null, userId, null, null, type: banType))
        {
            SharedUnban? unban = null;
            if (ban.Unban is { } unbanDef)
            {
                var unbanningAdmin = unbanDef.UnbanningAdmin == null
                    ? null
                    : (await _playerLocator.LookupIdAsync(unbanDef.UnbanningAdmin.Value))?.Username;
                unban = new SharedUnban(unbanningAdmin, ban.Unban.UnbanTime.UtcDateTime);
            }

            ImmutableArray<(string, int cidrMask)> ips = [("*Hidden*", 0)];
            ImmutableArray<string> hwids = ["*Hidden*"];

            if (BanManager.IsChatBanType(ban.Type)) // Pirate: chat ban list
            { // Pirate: chat ban list
                ips = []; // Pirate: chat ban list
                hwids = []; // Pirate: chat ban list
            } // Pirate: chat ban list
            else if (_admins.HasAdminFlag(Player, AdminFlags.Pii)) // Pirate: chat ban list
            {
                ips = [..ban.Addresses.Select(a => (a.address.ToString(), a.cidrMask))];
                hwids = [..ban.HWIds.Select(h => h.ToString())];
            }

            list.Add(new SharedBan(
                ban.Id,
                ban.Type,
                ban.UserIds,
                ips,
                hwids,
                ban.BanTime.UtcDateTime,
                ban.ExpirationTime?.UtcDateTime,
                ban.Reason,
                ban.BanningAdmin == null
                    ? null
                    : (await _playerLocator.LookupIdAsync(ban.BanningAdmin.Value))?.Username,
                unban,
                ban.Roles
            ));
        }
    }

    private async Task LoadFromDb()
    {
        Bans.Clear();
        RoleBans.Clear();
        ChatBans.Clear(); // Pirate: chat ban list

        var userId = new NetUserId(BanListPlayer);
        BanListPlayerName = (await _playerLocator.LookupIdAsync(userId))?.Username ??
                            string.Empty;

        await LoadBans(userId);

        StateDirty();
    }

    #region Pirate: chat ban list
    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not PardonChatBanRequest request || !_admins.HasAdminFlag(Player, AdminFlags.Ban))
            return;

        await _banManager.PardonChatBan(request.BanId, Player.UserId, DateTimeOffset.Now);
        await LoadFromDb();
    }
    #endregion Pirate: chat ban list

    public async Task ChangeBanListPlayer(Guid banListPlayer)
    {
        BanListPlayer = banListPlayer;
        await LoadFromDb();
    }
}
