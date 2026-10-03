// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Client.Administration.Managers;
using Content.Client.Administration.Systems;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Pirate.Administration.MentorHelp;
using Content.Shared._Pirate.CCVars;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client._Pirate.Administration.MentorHelp;

[UsedImplicitly]
public sealed class MentorHelpUIController : UIController,
    IOnSystemChanged<MentorHelpSystem>,
    IOnSystemChanged<BwoinkSystem>,
    IOnStateChanged<GameplayState>,
    IOnStateChanged<LobbyState>
{
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [UISystemDependency] private readonly AudioSystem _audio = default!;

    private MentorHelpSystem? _system;

    private Control? _content;
    private bool _contentIsResponder;

    private MentorHelpControl? _control;
    private readonly Dictionary<NetUserId, BwoinkPanel> _panels = new();

    private BwoinkPanel? _userPanel;
    private bool _discordRelayActive;

    private HelpTabs? _tabs;
    private bool _adminUnread;
    private bool _mentorUnread;
    private HelpWindowKind? _lastKind;

    private string? _aHelpSound;
    private bool _mentorHelpSoundEnabled;

    private MenuButton? GameAHelpButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.AHelpButton;
    private Button? LobbyAHelpButton => (UIManager.ActiveScreen as LobbyGui)?.AHelpButton;

    private AHelpUIController AHelp => UIManager.GetUIController<AHelpUIController>();

    private bool IsResponder => MentorHelpAccess.CanRespond(_adminManager.GetAdminData());

    public string MentorTitle => _mentorTitle ?? Loc.GetString("mentorhelp-window-title");

    private string? _mentorTitle;

    public HelpWindowKind PreferredKind => _lastKind ??
        (IsResponder && !_adminManager.HasFlag(AdminFlags.Adminhelp) ? HelpWindowKind.Mentor : HelpWindowKind.Admin);

    public override void Initialize()
    {
        base.Initialize();

        _adminManager.AdminStatusUpdated += OnAdminStatusUpdated;
        _config.OnValueChanged(CCVars.AHelpSound, v => _aHelpSound = v, true);
        // Keep the mentorhelp mute state independent of ahelp.
        _config.OnValueChanged(PirateVars.MentorHelpSoundEnabled, v => _mentorHelpSoundEnabled = v, true);
    }

    public void OnSystemLoaded(MentorHelpSystem system)
    {
        _system = system;
        _system.MessageReceived += OnMessageReceived;
        _system.TypingReceived += OnTypingReceived;
        _system.DiscordRelayChanged += OnDiscordRelayChanged;
    }

    public void OnSystemUnloaded(MentorHelpSystem system)
    {
        system.MessageReceived -= OnMessageReceived;
        system.TypingReceived -= OnTypingReceived;
        system.DiscordRelayChanged -= OnDiscordRelayChanged;
        _system = null;
    }

    public void OnSystemLoaded(BwoinkSystem system)
    {
        system.OnBwoinkTextMessageRecieved += OnAHelpReceived;
    }

    public void OnSystemUnloaded(BwoinkSystem system)
    {
        system.OnBwoinkTextMessageRecieved -= OnAHelpReceived;
    }

    #region Tabs

    public void RegisterTabs(HelpTabs tabs)
    {
        _tabs = tabs;
    }

    public void UnregisterTabs(HelpTabs tabs)
    {
        if (_tabs == tabs)
            _tabs = null;
    }

    public void OnTabSelected(HelpTabs tabs, HelpWindowKind kind)
    {
        // Construction can select a tab before the window opens; only opening it counts as read.
        if (!tabs.IsInsideTree)
            return;

        _lastKind = kind;
        if (kind == HelpWindowKind.Admin)
            _adminUnread = false;
        else
            _mentorUnread = false;

        RefreshMarkers();
        UpdateMenuButtons();
    }

    private void RefreshMarkers()
    {
        _tabs?.RefreshMarkers(_adminUnread, _mentorUnread);
    }

    public void OpenHelp(HelpWindowKind kind)
    {
        _lastKind = kind;

        var ahelp = AHelp;
        ahelp.EnsureUIHelper();
        if (ahelp.UIHelper is not { IsOpen: false } helper)
        {
            _tabs?.Select(kind);
            return;
        }

        if (helper.IsAdmin)
            helper.ToggleWindow();
        else
            ahelp.Open();
    }

    public void OpenTicket(NetUserId channel)
    {
        OpenHelp(HelpWindowKind.Mentor);
        if (_contentIsResponder)
        {
            EnsurePanel(channel);
            _control?.SelectChannel(channel);
        }
    }

    #endregion

    #region Messages

    private void OnMessageReceived(MentorHelpTextMessage message)
    {
        var local = _playerManager.LocalSession;
        if (local == null)
            return;

        if (message.PlaySound && local.UserId != message.TrueSender)
        {
            // Only responders can mute mentorhelp sounds.
            if (_aHelpSound != null && (_mentorHelpSoundEnabled || !IsResponder))
                _audio.PlayGlobal(_aHelpSound, Filter.Local(), false);
            _clyde.RequestWindowAttention();
        }

        GetMentorContent();

        var line = new SharedBwoinkSystem.BwoinkTextMessage(message.UserId, message.TrueSender, message.Text, message.SentAt, message.PlaySound);
        if (_contentIsResponder)
        {
            EnsurePanel(message.UserId).ReceiveLine(line);
            _control?.OnMessage();
        }
        else
        {
            _userPanel?.ReceiveLine(line);

            if (AHelp.UIHelper is not { IsOpen: true })
                OpenHelp(HelpWindowKind.Mentor);
        }

        if (_tabs == null || !_tabs.IsShowing(HelpWindowKind.Mentor))
        {
            _mentorUnread = true;
            RefreshMarkers();
            UpdateMenuButtons();
        }
    }

    private void OnAHelpReceived(object? sender, SharedBwoinkSystem.BwoinkTextMessage message)
    {
        if (!_adminManager.HasFlag(AdminFlags.Adminhelp))
        {
            _lastKind = HelpWindowKind.Admin;
            _tabs?.Select(HelpWindowKind.Admin);
            return;
        }

        if (_tabs == null || !_tabs.IsShowing(HelpWindowKind.Admin))
        {
            _adminUnread = true;
            RefreshMarkers();
        }
    }

    private void OnDiscordRelayChanged(bool enabled)
    {
        _discordRelayActive = enabled;
        if (_userPanel != null)
            _userPanel.RelayedToDiscordLabel.Visible = enabled;
    }

    private void OnTypingReceived(MentorHelpPlayerTypingUpdated args)
    {
        if (_panels.TryGetValue(args.Channel, out var panel))
            panel.UpdatePlayerTyping(args.PlayerName, args.Typing);
    }

    #endregion

    #region Content

    private void OnAdminStatusUpdated()
    {
        if (_content == null || _contentIsResponder == IsResponder)
            return;

        DisposeContent();
        if (_tabs != null)
            _tabs.SetMentorContent(GetMentorContent());
    }

    public Control GetMentorContent()
    {
        if (_content is { Disposed: false } && _contentIsResponder == IsResponder)
            return _content;

        DisposeContent();

        var owner = _playerManager.LocalUser!.Value;
        _contentIsResponder = IsResponder;

        if (_contentIsResponder)
        {
            _control = new MentorHelpControl(this);
            _content = _control;
        }
        else
        {
            _userPanel = new BwoinkPanel(text => _system?.Send(owner, text, true));
            _userPanel.InputTextChanged += text => _system?.SendTyping(owner, text.Length > 0);
            _userPanel.RelayedToDiscordLabel.Visible = _discordRelayActive;
            _userPanel.ReceiveLine(new SharedBwoinkSystem.BwoinkTextMessage(owner, SharedBwoinkSystem.SystemUserId,
                Loc.GetString("mentorhelp-introductory-message")));
            _content = _userPanel;
        }

        return _content;
    }

    private void DisposeContent()
    {
        _content?.Orphan();
        _content?.Dispose();
        _content = null;
        _control = null;
        _userPanel = null;
        _panels.Clear();
        _mentorTitle = null;
    }

    public BwoinkPanel EnsurePanel(NetUserId channel)
    {
        if (_panels.TryGetValue(channel, out var panel))
            return panel;

        panel = new BwoinkPanel(text => _system?.Send(channel, text,
            _control?.PlaySound.Pressed ?? true,
            _control?.ResponderOnly.Pressed ?? false))
        {
            Visible = false,
            VerticalExpand = true,
        };
        panel.InputTextChanged += text => _system?.SendTyping(channel, text.Length > 0);
        _panels[channel] = panel;
        _control?.HelpArea.AddChild(panel);
        return panel;
    }

    public bool TryGetPanel(NetUserId channel, [NotNullWhen(true)] out BwoinkPanel? panel)
    {
        return _panels.TryGetValue(channel, out panel);
    }

    public void OnPlayerSelected(PlayerInfo? player)
    {
        if (player == null)
        {
            _mentorTitle = null;
        }
        else
        {
            var playtime = player.OverallPlaytime != null ? player.PlaytimeString : Loc.GetString("generic-unknown-title");
            _mentorTitle = $"{player.CharacterName} / {player.Username} | {Loc.GetString("generic-playtime-title")}: {playtime}";
        }

        _tabs?.RefreshMentorTitle();
        ShowPanel(player?.SessionId);
    }

    public void ShowPanel(NetUserId? channel)
    {
        foreach (var panel in _panels.Values)
        {
            panel.Visible = false;
        }

        if (channel != null)
            EnsurePanel(channel.Value).Visible = true;
    }

    #endregion

    #region Menu buttons

    public void OnStateEntered(GameplayState state) => UIManager.DeferAction(UpdateMenuButtons);
    public void OnStateExited(GameplayState state) { }
    public void OnStateEntered(LobbyState state) => UIManager.DeferAction(UpdateMenuButtons);
    public void OnStateExited(LobbyState state) { }

    // AHelp resets the button on close; wait a frame before restoring unread state.
    public void OnHelpWindowClosed()
    {
        UIManager.DeferAction(UpdateMenuButtons);
    }

    private void UpdateMenuButtons()
    {
        var open = AHelp.UIHelper is { IsOpen: true };
        foreach (var button in new Control?[] { GameAHelpButton, LobbyAHelpButton })
        {
            if (button == null)
                continue;

            if (!open && _mentorUnread)
                button.AddStyleClass(HelpTabs.StyleClassMentorUnread);
            else
                button.RemoveStyleClass(HelpTabs.StyleClassMentorUnread);

            // AHelp clears its red unread state on close; restore it if the ahelp tab remains unread.
            if (!open && _adminUnread)
                button.AddStyleClass(StyleClass.Negative);
        }
    }

    #endregion
}
