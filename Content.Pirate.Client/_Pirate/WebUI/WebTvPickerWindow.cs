// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Pirate.Shared.TV;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     The TV's picker: a YouTube-only browser used to find a video. The
///     host fence (<see cref="WebTvChannel.AllowHosts"/>) keeps it inside
///     YouTube; clicking a video here lets you confirm and it becomes the
///     TV's channel or a queue entry. Opens from the TV window.
/// </summary>
public sealed class WebTvPickerWindow : DefaultWindow, IDisposable
{
    private readonly WebViewControl _web;
    private readonly WebUiTuiIpc _ipc;

    private readonly Label _here = new()
    {
        Text = "шукай відео на YouTube, тоді натисни «Обрати»",
        FontColorOverride = Color.LightGray,
    };

    private readonly Button _watch = new() { Text = "▶ Поставити на ТБ", Disabled = true };
    private readonly Button _queueAdd = new() { Text = "➕ В чергу", Disabled = true };

    private readonly PanelContainer _confirmPanel = new()
    {
        Visible = false,
        PanelOverride = new StyleBoxFlat(new Color(70, 40, 22)),
    };

    private readonly Label _confirmText = new()
    {
        Text = "",
        FontColorOverride = Color.Orange,
    };

    private readonly Button _confirmYes = new() { Text = "Так, переключити" };
    private readonly Button _confirmNo = new() { Text = "Ні, гляну ще" };

    // Page the user is currently browsing (tracked from the live URL).
    private string _browsedUrl = "";
    private string _pendingPlaybackUrl = "";
    private string _pendingLabel = "";
    private WebTvChannel.WebTvKind _pendingKind = WebTvChannel.WebTvKind.None;
    private Label? _currentStatusLabel;
    private Label? _pickerQueueTitle;
    private BoxContainer? _pickerQueueBox;
    private bool _disposed;

    /// <summary>The TV entity this picker drives; far away ⇒ auto-close.</summary>
    public EntityUid? TvUid;

    public WebTvPickerWindow()
    {
        Title = "Pirate TV — Обрати відео";
        SetSize = new Vector2i(1120, 660);

        _web = new WebViewControl
        {
            AlwaysActive = true,
        };
        _ipc = new WebUiTuiIpc((_, _) => { })
        {
            AllowHttpHosts = new System.Collections.Generic.List<string>(WebTvChannel.AllowHosts),
        };
        _web.AddBeforeBrowseHandler(_ipc.HandleBeforeBrowse);

        _web.VerticalExpand = true;
        _web.HorizontalExpand = true;

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            MinWidth = 360,
            MaxWidth = 380,
            Margin = new Thickness(8),
        };
        right.AddChild(new Label
        {
            Text = "Що дивитимемо?",
            FontColorOverride = Color.Gold,
        });
        _here.ClipText = true;
        right.AddChild(_here);
        right.AddChild(_watch);
        right.AddChild(_queueAdd);

        var confirmBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
        };
        confirmBox.AddChild(_confirmText);
        confirmBox.AddChild(_confirmYes);
        confirmBox.AddChild(_confirmNo);
        _confirmPanel.AddChild(confirmBox);
        right.AddChild(_confirmPanel);

        var s = Snapshot();
        _currentStatusLabel = new Label
        {
            Text = s.Kind == WebTvChannel.WebTvKind.None
                ? "Зараз на ТБ: (нічого не грає)"
                : "Зараз на ТБ: " + s.Label,
            FontColorOverride = Color.Gray,
            ClipText = true,
        };
        right.AddChild(_currentStatusLabel);

        _pickerQueueTitle = new Label
        {
            Text = "Черга:",
            FontColorOverride = Color.Gold,
        };
        _pickerQueueBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };
        right.AddChild(_pickerQueueTitle);
        right.AddChild(_pickerQueueBox);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        box.AddChild(_web);
        box.AddChild(right);

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(new Color(20, 26, 34)),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(box);
        Contents.AddChild(panel);

        // Start on YouTube's search page (whitelist keeps it inside).
        try { _web.Url = "https://www.youtube.com/results?search_query=music&hl=uk"; } catch { /* headless dev */ }

        _watch.OnPressed += OnWatchPressed;
        _queueAdd.OnPressed += OnQueueAddPressed;
        _confirmYes.OnPressed += OnConfirmYes;
        _confirmNo.OnPressed += OnConfirmNo;

        // The engine keeps AlwaysActive browsers alive past window closes;
        // drain the browser here so no audio keeps spilling in background.
        OnClose += () => { try { _web.AlwaysActive = false; } catch { } };
    }

    public void OpenCenteredPicker()
    {
        OpenCentered();
        _confirmPanel.Visible = false;
        RefreshRoom();
    }

    private PirateTvClientState.Entry Snapshot()
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return new PirateTvClientState.Entry();
        return PirateTvClientState.Get(PirateTvClientState.Net(TvUid.Value));
    }

    private NetEntity TvNet()
        => TvUid != null && TvUid.Value.IsValid() ? PirateTvClientState.Net(TvUid.Value) : NetEntity.Invalid;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!PirateTvClientState.Connected)
        {
            Close();
            return;
        }

        if (TvUid == null || !TvUid.Value.IsValid())
        {
            Close();
            return;
        }

        PirateTvClientState.Request(TvNet());
        _ipc.Pump();
        RefreshRoom();
        RangeCheck();

        // Track what the user is on right now (fence keeps hosts legal).
        try
        {
            var url = _web.Url;
            if (url != _browsedUrl)
            {
                _browsedUrl = url;
                ReevaluateHere(url);
            }
        }
        catch { /* headless dev engines */ }
    }

    private void RangeCheck()
    {
        if (TvUid == null || !TvUid.Value.IsValid())
            return;
        try
        {
            var ent = IoCManager.Resolve<Robust.Shared.GameObjects.IEntityManager>();
            var player = IoCManager.Resolve<Robust.Client.Player.IPlayerManager>().LocalEntity;
            if (player == null)
                return;
            if (!ent.TryGetComponent(player.Value, out Robust.Shared.GameObjects.TransformComponent? pt) ||
                !ent.TryGetComponent(TvUid.Value, out Robust.Shared.GameObjects.TransformComponent? tt) ||
                pt.MapID != tt.MapID)
            {
                return;
            }
            var dv = pt.MapPosition.Position - tt.MapPosition.Position;
            if (dv.Length() > 10.0)
                Close();
        }
        catch { }
    }

    private void ReevaluateHere(string url)
    {
        _confirmPanel.Visible = false;
        _watch.Disabled = true;
        _here.Text = url;
        _here.FontColorOverride = Color.LightGray;

        var ok = WebTvChannel.TryBuild(url, out var kind, out var playback, out var label);
        Robust.Shared.Log.Logger.DebugS("webui.tv", $"here: {url} parse={ok} kind={kind} play={playback}");

        if (ok)
        {
            var locked = Snapshot().Locked;
            _here.Text = $"{label}: ✓ готово до перегляду" + (locked ? " (ТБ замкнено)" : "");
            _here.FontColorOverride = locked ? Color.Red : Color.LightGreen;
            _watch.Disabled = locked;
            _queueAdd.Disabled = locked;
            _pendingPlaybackUrl = playback;
            _pendingLabel = label;
            _pendingKind = kind;
        }
        else
        {
            _watch.Disabled = true;
            _queueAdd.Disabled = true;
            _pendingPlaybackUrl = "";
        }
    }

    // ===== pick / confirm / apply =====

    private void OnWatchPressed(BaseButton.ButtonEventArgs _) => PickPending();
    private void OnConfirmYes(BaseButton.ButtonEventArgs _) { Apply(); _confirmPanel.Visible = false; }
    private void OnConfirmNo(BaseButton.ButtonEventArgs _) => _confirmPanel.Visible = false;
    private void OnQueueAddPressed(BaseButton.ButtonEventArgs _) => PerformQueueAdd();

    private void PickPending()
    {
        if (_pendingPlaybackUrl.Length == 0)
            return;

        var s = Snapshot();
        if (WebTvChannel.NeedsConfirm(s, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
        {
            _confirmText.Text = "Зараз на ТБ грає: " + s.Label;
            _confirmPanel.Visible = true;
        }
        else
        {
            Apply();
        }
    }

    // ===== queue list + live lock gate (mirrors the TV window) =====

    private void RefreshRoom()
    {
        var s = Snapshot();
        var locked = s.Locked;

        if (_pendingPlaybackUrl.Length > 0)
        {
            _watch.Disabled = locked;
            _queueAdd.Disabled = locked;
        }

        if (_currentStatusLabel != null)
        {
            _currentStatusLabel.Text = s.Kind == WebTvChannel.WebTvKind.None
                ? "Зараз на ТБ: (нічого не грає)"
                : "Зараз на ТБ: " + s.Label;
        }

        if (_pickerQueueBox == null)
            return;

        _pickerQueueTitle!.Text = "Черга (" + s.Queue.Count + ")" + (locked ? " [замкнено]" : "");
        _pickerQueueBox.RemoveAllChildren();
        var idx = 0;
        var tv = TvNet();
        foreach (var item in s.Queue)
        {
            var i = idx++;
            var title = item.Title.Length > 0 ? item.Title : item.Label;
            _pickerQueueBox.AddChild(WebTvWindow.BuildQueueRow(i, title, i == s.QueueNow, locked,
                ev => SendQueue(ev, tv)));
        }
    }

    private static void SendQueue(EntityEventArgs ev, NetEntity tv)
    {
        switch (ev)
        {
            case PirateTvQueueNavEvent n: n.Tv = tv; PirateTvClientState.Send(n); break;
            case PirateTvQueueMoveEvent m: m.Tv = tv; PirateTvClientState.Send(m); break;
            case PirateTvQueueRemoveEvent r: r.Tv = tv; PirateTvClientState.Send(r); break;
        }
    }

    /// <summary>Add the currently ready video to the TV's playlist.</summary>
    private void PerformQueueAdd()
    {
        if (_pendingPlaybackUrl.Length == 0)
            return;

        PirateTvClientState.Send(new PirateTvQueueAddEvent
        {
            Tv = TvNet(),
            Url = _pendingPlaybackUrl,
            Kind = (int)_pendingKind,
            Label = _pendingLabel,
        });

        _here.Text = "➕ Додано в чергу";
        _here.FontColorOverride = Color.Orange;
    }

    /// <summary>Moves this TV's (or its group's) clock to the new channel.</summary>
    private void Apply()
    {
        if (_pendingPlaybackUrl.Length == 0)
            return;

        PirateTvClientState.Send(new PirateTvPickEvent
        {
            Tv = TvNet(),
            Url = _pendingPlaybackUrl,
            Kind = (int)_pendingKind,
            Label = _pendingLabel,
        });

        if (_currentStatusLabel != null)
            _currentStatusLabel.Text = "Зараз на ТБ: " + _pendingLabel;
        _here.Text = "✔ Поставлено на ТБ";
        _here.FontColorOverride = Color.Orange;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
