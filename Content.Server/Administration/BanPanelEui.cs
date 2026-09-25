// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Server.Administration;

public sealed class BanPanelEui : BaseEui
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPlayerLocator _playerLocator = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IAdminManager _admins = default!;

    private readonly ISawmill _sawmill;

    private NetUserId? PlayerId { get; set; }
    private string PlayerName { get; set; } = string.Empty;
    private IPAddress? LastAddress { get; set; }
    private ImmutableTypedHwid? LastHwid { get; set; }
    private const int Ipv4_CIDR = CreateBanInfo.DefaultMaskIpv4;
    private const int Ipv6_CIDR = CreateBanInfo.DefaultMaskIpv6;

    public BanPanelEui()
    {
        IoCManager.InjectDependencies(this);

        _sawmill = _log.GetSawmill("admin.bans_eui");
    }

    public override EuiStateBase GetNewState()
    {
        var hasBan = _admins.HasAdminFlag(Player, AdminFlags.Ban);
        return new BanPanelEuiState(PlayerName, hasBan);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case BanPanelEuiStateMsg.CreateBanRequest r:
                BanPlayer(r.Ban);
                break;
            case BanPanelEuiStateMsg.GetPlayerInfoRequest r:
                ChangePlayer(r.PlayerUsername);
                break;
        }
    }

    private async void BanPlayer(Ban ban)
    {
        if (!_admins.HasAdminFlag(Player, AdminFlags.Ban))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to create a ban with no ban flag");

            return;
        }

        if (ban.Target == null && string.IsNullOrWhiteSpace(ban.IpAddress) && ban.Hwid == null)
        {
            _chat.DispatchServerMessage(Player, Loc.GetString("ban-panel-no-data"));

            return;
        }

        var isRoleBan = ban.Type == BanType.Role; // Pirate: chat ban panel
        var isChatBan = BanManager.IsChatBanType(ban.Type); // Pirate: chat ban panel

        if (isChatBan && ban.Target == null) // Pirate: chat ban panel
        { // Pirate: chat ban panel
            _chat.DispatchServerMessage(Player, Loc.GetString("ban-panel-no-data")); // Pirate: chat ban panel
            return; // Pirate: chat ban panel
        } // Pirate: chat ban panel

        #region Pirate: chat ban panel
        CreateBanInfo banInfo = ban.Type switch
        {
            BanType.Server => new CreateServerBanInfo(ban.Reason),
            BanType.Role => new CreateRoleBanInfo(ban.Reason),
            BanType.OOC or BanType.LOOC or BanType.Deadchat => new CreateChatBanInfo(ban.Type, ban.Reason),
            _ => throw new ArgumentOutOfRangeException(nameof(ban.Type), ban.Type, "Unknown ban type"),
        };
        #endregion Pirate: chat ban panel

        banInfo.WithBanningAdmin(Player.UserId);
        banInfo.WithSeverity(ban.Severity);
        if (ban.BanDurationMinutes > 0)
            banInfo.WithMinutes(ban.BanDurationMinutes);

        (IPAddress, int)? addressRange = null;
        if (!isChatBan && ban.IpAddress is not null) // Pirate: chat ban panel
        {
            if (!IPAddress.TryParse(ban.IpAddress, out var ipAddress) || !uint.TryParse(ban.IpAddressHid, out var hidInt) || hidInt > Ipv6_CIDR || hidInt > Ipv4_CIDR && ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                _chat.DispatchServerMessage(Player, Loc.GetString("ban-panel-invalid-ip"));
                return;
            }

            if (hidInt == 0)
                hidInt = (uint) (ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR);

            addressRange = (ipAddress, (int) hidInt);
        }

        var targetUid = ban.Target is not null ? PlayerId : null;
        addressRange = !isChatBan && ban.UseLastIp && LastAddress is not null ? (LastAddress, LastAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR) : addressRange; // Pirate: chat ban panel
        var targetHWid = isChatBan ? null : ban.UseLastHwid ? LastHwid : ban.Hwid; // Pirate: chat ban panel
        if (ban.Target != null && (ban.Target != PlayerName || Guid.TryParse(ban.Target, out var parsed) && parsed != PlayerId)) // Pirate: chat ban panel
        {
            var located = await _playerLocator.LookupIdByNameOrIdAsync(ban.Target);
            if (located == null)
            {
                _chat.DispatchServerMessage(Player, Loc.GetString("cmd-ban-player"));
                return;
            }
            targetUid = located.UserId;
            var targetAddress = located.LastAddress;
            if (!isChatBan && ban.UseLastIp && targetAddress != null) // Pirate: chat ban panel
            {
                if (targetAddress.IsIPv4MappedToIPv6)
                    targetAddress = targetAddress.MapToIPv4();

                // Ban /64 for IPv6, /32 for IPv4.
                var hid = targetAddress.AddressFamily == AddressFamily.InterNetworkV6 ? Ipv6_CIDR : Ipv4_CIDR;
                addressRange = (targetAddress, hid);
            }
            targetHWid = isChatBan ? null : ban.UseLastHwid ? located.LastHWId : ban.Hwid; // Pirate: chat ban panel
        }

        #region Pirate: chat ban panel
        if (isChatBan && targetUid == null)
        {
            var located = await _playerLocator.LookupIdByNameOrIdAsync(ban.Target!);
            if (located == null)
            {
                _chat.DispatchServerMessage(Player, Loc.GetString("cmd-ban-player"));
                return;
            }

            targetUid = located.UserId;
        }

        if (!_admins.HasAdminFlag(Player, AdminFlags.Ban))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) lost the ban flag while creating a ban");
            return;
        }
        #endregion Pirate: chat ban panel

        if (!isChatBan && addressRange != null) // Pirate: chat ban panel
            banInfo.AddAddressRange(addressRange.Value);

        if (targetUid != null)
            banInfo.AddUser(targetUid.Value, ban.Target!);

        if (!isChatBan) // Pirate: chat ban panel
            banInfo.AddHWId(targetHWid); // Pirate: chat ban panel

        if (isRoleBan)
        {
            var roleBanInfo = (CreateRoleBanInfo)banInfo;
            foreach (var row in ban.BannedJobs ?? [])
            {
                roleBanInfo.AddJob(row);
            }

            foreach (var row in ban.BannedAntags ?? [])
            {
                roleBanInfo.AddAntag(row);
            }

            _banManager.CreateRoleBan(roleBanInfo);
        }
        else if (isChatBan) // Pirate: chat ban panel
        { // Pirate: chat ban panel
            await _banManager.CreateChatBan((CreateChatBanInfo) banInfo); // Pirate: chat ban panel
        } // Pirate: chat ban panel
        else
        {
            if (ban.Erase && targetUid is not null)
            {
                try
                {
                    if (_entities.TrySystem(out AdminSystem? adminSystem))
                        adminSystem.Erase(targetUid.Value);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Error while erasing banned player:\n{e}");
                }
            }

            _banManager.CreateServerBan((CreateServerBanInfo)banInfo);
        }

        Close();
    }

    public async void ChangePlayer(string playerNameOrId)
    {
        var located = await _playerLocator.LookupIdByNameOrIdAsync(playerNameOrId);
        ChangePlayer(located?.UserId, located?.Username ?? string.Empty, located?.LastAddress, located?.LastHWId);
    }

    public void ChangePlayer(NetUserId? playerId, string playerName, IPAddress? lastAddress, ImmutableTypedHwid? lastHwid)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        LastAddress = lastAddress;
        LastHwid = lastHwid;
        StateDirty();
    }

    public override async void Opened()
    {
        base.Opened();
        _admins.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();
        _admins.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player)
        {
            return;
        }

        StateDirty();
    }
}
