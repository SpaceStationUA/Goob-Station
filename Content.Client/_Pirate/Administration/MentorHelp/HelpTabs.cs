// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Administration.UI.Bwoink;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Stylesheets;
using Content.Shared._Pirate.Chat;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;

namespace Content.Client._Pirate.Administration.MentorHelp;

public enum HelpWindowKind : byte
{
    Admin,
    Mentor,
}

// TabContainer styles every tab together, so unread tabs need separate controls.
public sealed class HelpTabs : BoxContainer
{
    public const string StyleClassMentorWindowHeader = "windowHeaderMentor";
    public const string StyleClassPanel = "HelpTabsPanel";
    public const string StyleClassMentorUnread = "MentorHelpUnread";
    public const string StyleClassTab = "HelpTab";
    public const string StyleClassTabActive = "HelpTabActive";
    public const string StyleClassTabUnreadAdmin = "HelpTabUnreadAdmin";
    public const string StyleClassTabUnreadMentor = "HelpTabUnreadMentor";

    public static readonly Color AdminColor = Color.FromHex("#96001e");

    public static Color MentorColor => StaffChats.Mentor.Color;

    private readonly DefaultWindow _window;
    private readonly Control _adminContent;
    private readonly PanelContainer _panel;
    private readonly BoxContainer _contents;
    private readonly Tab _adminTab;
    private readonly Tab _mentorTab;
    private Control? _mentorContent;
    private string? _adminTitle;
    private bool _adminUnread;
    private bool _mentorUnread;

    public HelpWindowKind Current { get; private set; } = HelpWindowKind.Admin;

    private static MentorHelpUIController Controller =>
        IoCManager.Resolve<IUserInterfaceManager>().GetUIController<MentorHelpUIController>();

    public static HelpTabs Install(DefaultWindow window, Control ahelpContent)
    {
        ahelpContent.Orphan();
        var tabs = new HelpTabs(window, ahelpContent);
        window.Contents.AddChild(tabs);
        return tabs;
    }

    private HelpTabs(DefaultWindow window, Control adminContent)
    {
        _window = window;
        _adminContent = adminContent;

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        _adminTab = new Tab(() => Select(HelpWindowKind.Admin));
        _mentorTab = new Tab(() => Select(HelpWindowKind.Mentor));

        var strip = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
        strip.AddChild(_adminTab);
        strip.AddChild(_mentorTab);

        _contents = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _panel.AddChild(_contents);

        adminContent.HorizontalExpand = true;
        adminContent.VerticalExpand = true;
        _contents.AddChild(adminContent);
        if (adminContent is BwoinkControl bwoink)
            HideTopSpacer(bwoink.ChannelSelector);

        AddChild(strip);
        AddChild(_panel);

        Controller.RegisterTabs(this);
        SetMentorContent(Controller.GetMentorContent());
        Select(Controller.PreferredKind);
    }

    public void SetMentorContent(Control content)
    {
        if (_mentorContent == content)
            return;

        _mentorContent?.Orphan();
        content.Orphan();
        content.HorizontalExpand = true;
        content.VerticalExpand = true;
        content.Visible = Current == HelpWindowKind.Mentor;
        _mentorContent = content;
        _contents.AddChild(content);
        if (content is MentorHelpControl mentor)
            HideTopSpacer(mentor.ChannelSelector);
        UpdatePanel();
    }

    private static void HideTopSpacer(PlayerListControl list)
    {
        if (list.ChildCount > 0 && list.GetChild(0) is var spacer && spacer.GetType() == typeof(Control))
            spacer.Visible = false;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        Select(Controller.PreferredKind);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        Controller.OnHelpWindowClosed();
    }

    public void Select(HelpWindowKind kind)
    {
        var changed = Current != kind;
        Current = kind;

        _adminContent.Visible = kind == HelpWindowKind.Admin;
        if (_mentorContent != null)
            _mentorContent.Visible = kind == HelpWindowKind.Mentor;

        if (kind == HelpWindowKind.Mentor)
        {
            if (changed || _adminTitle == null)
                _adminTitle = _window.Title;

            _window.Title = Controller.MentorTitle;
            _window.HeaderClass = StyleClassMentorWindowHeader;
        }
        else
        {
            if (changed && _adminTitle != null)
                _window.Title = _adminTitle;

            _window.HeaderClass = StyleClass.AlertWindowHeader;
        }

        UpdatePanel();
        Controller.OnTabSelected(this, kind);
        UpdateTabs();
    }

    private void UpdatePanel()
    {
        var shown = Current == HelpWindowKind.Admin ? _adminContent : _mentorContent;
        var bordered = shown is BwoinkPanel;

        if (bordered)
            _panel.AddStyleClass(StyleClassPanel);
        else
            _panel.RemoveStyleClass(StyleClassPanel);

        _contents.Margin = bordered ? new Thickness(0, 4, 0, 0) : default;
    }

    public void RefreshMentorTitle()
    {
        if (Current == HelpWindowKind.Mentor)
            _window.Title = Controller.MentorTitle;
    }

    public bool IsShowing(HelpWindowKind kind)
    {
        return Current == kind && _window is { Disposed: false, IsOpen: true };
    }

    public void RefreshMarkers(bool adminUnread, bool mentorUnread)
    {
        _adminUnread = adminUnread;
        _mentorUnread = mentorUnread;
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        _adminTab.Update(Loc.GetString("help-tab-admin"), Current == HelpWindowKind.Admin,
            _adminUnread ? StyleClassTabUnreadAdmin : null);
        _mentorTab.Update(Loc.GetString("help-tab-mentor"), Current == HelpWindowKind.Mentor,
            _mentorUnread ? StyleClassTabUnreadMentor : null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // This control is reused; detach it before the window disposes its children.
            _mentorContent?.Orphan();
            _mentorContent = null;
            Controller.UnregisterTabs(this);
        }

        base.Dispose(disposing);
    }

    private sealed class Tab : PanelContainer
    {
        private readonly Label _label;

        public Tab(Action onClick)
        {
            MouseFilter = MouseFilterMode.Stop;
            DefaultCursorShape = CursorShape.Pointer;
            StyleClasses.Add(StyleClassTab);

            _label = new Label();
            AddChild(_label);

            OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                    return;

                args.Handle();
                onClick();
            };
        }

        public void Update(string title, bool active, string? unreadClass)
        {
            SetClass(StyleClassTabActive, active && unreadClass == null);
            SetClass(StyleClassTabUnreadAdmin, unreadClass == StyleClassTabUnreadAdmin);
            SetClass(StyleClassTabUnreadMentor, unreadClass == StyleClassTabUnreadMentor);

            _label.Text = unreadClass != null ? $"{title} ●" : $"{title}   ";
            _label.FontColorOverride = active || unreadClass != null ? Color.White : Color.Gray;
        }

        private void SetClass(string styleClass, bool set)
        {
            if (set)
                AddStyleClass(styleClass);
            else
                RemoveStyleClass(styleClass);
        }
    }
}
