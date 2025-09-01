using Content.Client.Administration.UI.Bwoink;
using Content.Shared.Administration;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;

namespace Content.Client.UserInterface.Systems.Bwoink;

// Pirate Changes Start Here - User bwoink panel with tabs
public sealed partial class UserBwoinkPanel : Control
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public TabContainer MainTabContainer { get; private set; } = default!;
    public BoxContainer IndividualChatContainer { get; private set; } = default!;
    public TabContainer GroupTabContainer { get; private set; } = default!;

    // Individual chat controls
    public OutputPanel IndividualTextOutput { get; private set; } = default!;
    public RichTextLabel IndividualTypingIndicator { get; private set; } = default!;
    public HistoryLineEdit IndividualSenderLineEdit { get; private set; } = default!;
    public RichTextLabel IndividualRelayedToDiscordLabel { get; private set; } = default!;

    private readonly Dictionary<Guid, BwoinkPanel> _groupPanels = new();
    private readonly Dictionary<Guid, BwoinkGroupInfo> _groups = new();
    private NetUserId _ownerId;
    private bool _discordRelayActive;

    public UserBwoinkPanel()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        // Initialize controls from XAML
        MainTabContainer = FindControl<TabContainer>("MainTabContainer");
        IndividualChatContainer = FindControl<BoxContainer>("IndividualChatContainer");
        GroupTabContainer = FindControl<TabContainer>("GroupTabContainer");

        // Initialize individual chat controls
        IndividualTextOutput = FindControl<OutputPanel>("IndividualTextOutput");
        IndividualTypingIndicator = FindControl<RichTextLabel>("IndividualTypingIndicator");
        IndividualSenderLineEdit = FindControl<HistoryLineEdit>("IndividualSenderLineEdit");
        IndividualRelayedToDiscordLabel = FindControl<RichTextLabel>("IndividualRelayedToDiscordLabel");
    }

    public void Initialize(NetUserId ownerId, bool discordRelayActive)
    {
        _ownerId = ownerId;
        _discordRelayActive = discordRelayActive;

        // Set tab titles
        MainTabContainer.SetTabTitle(0, Loc.GetString("bwoink-tab-ahelp"));
        MainTabContainer.SetTabTitle(1, Loc.GetString("bwoink-tab-groups"));

        // Setup individual chat
        SetupIndividualChat();

        // Initially hide groups tab if no groups
        UpdateGroupsVisibility();
    }

    private void SetupIndividualChat()
    {
        // Setup Discord relay label
        IndividualRelayedToDiscordLabel.Visible = _discordRelayActive;
        IndividualRelayedToDiscordLabel.SetMessage(FormattedMessage.FromMarkup(Loc.GetString("bwoink-system-discord-relay")));

        // Setup typing indicator
        IndividualTypingIndicator.Visible = false;

        // Setup line edit for sending messages
        IndividualSenderLineEdit.OnTextEntered += OnIndividualMessageSent;
        IndividualSenderLineEdit.PlaceHolder = Loc.GetString("bwoink-system-type-message");

        // Add introductory message
        var introText = Loc.GetString("bwoink-system-introductory-message");
        var introMessage = new SharedBwoinkSystem.BwoinkTextMessage(_ownerId, SharedBwoinkSystem.SystemUserId, introText);
        ReceiveIndividualMessage(introMessage);
    }

    private void OnIndividualMessageSent(LineEdit.LineEditEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Text))
            return;

        // Use the controller's send action
        var uiController = IoCManager.Resolve<IUserInterfaceManager>().GetUIController<AHelpUIController>();
        uiController.UIHelper?.SendMessageAction?.Invoke(_ownerId, args.Text, true, false);

        // Clear the input
        IndividualSenderLineEdit.Clear();
    }

    public void ReceiveIndividualMessage(SharedBwoinkSystem.BwoinkTextMessage message)
    {
        // Format and display the message like BwoinkPanel does
        var formatted = new FormattedMessage(1);
        formatted.AddMarkupOrThrow($"[color=gray]{message.SentAt.ToShortTimeString()}[/color] {message.Text}");
        IndividualTextOutput.AddMessage(formatted);

        // Scroll to bottom
        IndividualTextOutput.ScrollToBottom();
    }

    public void ReceiveGroupMessage(BwoinkGroupTextMessage message)
    {
        if (!_groups.ContainsKey(message.GroupId))
            return;

        if (!_groupPanels.TryGetValue(message.GroupId, out var panel))
            return;

        // Message already formatted by server with sender name and admin colors
        var formattedMessage = new SharedBwoinkSystem.BwoinkTextMessage(
            message.SenderId,
            message.SenderId,
            message.Text, // Already contains sender name and colors from server
            message.SentAt,
            message.PlaySound
        );

        panel.ReceiveLine(formattedMessage);
    }

    public void UpdateGroups(Dictionary<Guid, BwoinkGroupInfo> groups)
    {
        _groups.Clear();
        foreach (var (groupId, groupInfo) in groups)
        {
            // Only add groups where the player is a member
            if (groupInfo.Members.Contains(_ownerId))
            {
                _groups[groupId] = groupInfo;
            }
        }

        UpdateGroupTabs();
        UpdateGroupsVisibility();
    }

    private void UpdateGroupTabs()
    {
        // Clear existing group tabs
        GroupTabContainer.RemoveAllChildren();
        foreach (var panel in _groupPanels.Values)
        {
            panel.Dispose();
        }
        _groupPanels.Clear();

        // Add tabs for each group
        foreach (var (groupId, groupInfo) in _groups)
        {
            var panel = new BwoinkPanel(text =>
            {
                // Use the bwoink system directly for group messages
                var bwoinkSystem = _entityManager.System<Administration.Systems.BwoinkSystem>();
                bwoinkSystem.SendGroupMessage(groupId, text, true);
            });

            panel.RelayedToDiscordLabel.Visible = _discordRelayActive;
            _groupPanels[groupId] = panel;
            GroupTabContainer.AddChild(panel);
            GroupTabContainer.SetTabTitle(GroupTabContainer.ChildCount - 1, groupInfo.GroupName);
        }

        // If no groups, show "no groups" message
        if (_groups.Count == 0)
        {
            var noGroupsLabel = new Label
            {
                Text = Loc.GetString("group-chat-no-groups"),
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center
            };
            GroupTabContainer.AddChild(noGroupsLabel);
            GroupTabContainer.SetTabTitle(0, Loc.GetString("group-chat-no-groups"));
        }
    }

    private void UpdateGroupsVisibility()
    {
        // Show/hide groups tab based on whether player has groups
        if (_groups.Count > 0)
        {
            MainTabContainer.SetTabVisible(1, true);
        }
        else
        {
            MainTabContainer.SetTabVisible(1, false);
            // Switch to individual chat tab if groups tab was active
            MainTabContainer.CurrentTab = 0;
        }
    }

    public void DiscordRelayChanged(bool active)
    {
        _discordRelayActive = active;

        IndividualRelayedToDiscordLabel.Visible = active;

        foreach (var panel in _groupPanels.Values)
        {
            panel.RelayedToDiscordLabel.Visible = active;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            IndividualSenderLineEdit.OnTextEntered -= OnIndividualMessageSent;

            foreach (var panel in _groupPanels.Values)
            {
                panel.Dispose();
            }
            _groupPanels.Clear();
            _groups.Clear();
        }
    }
}
// Pirate Changes End Here
