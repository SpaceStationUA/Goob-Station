// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Content.Pirate.Shared.TV;
using Robust.Shared.Timing;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     The TV's browser: someone browses (YouTube/Twitch only — the fence
///     is the policy) and presses "Поставити на ТБ". If the room currently
///     watches something, a confirm step appears first; confirming swaps
///     the shared channel and every open TV window follows.
/// </summary>
public sealed class WebTvPickerWindow : DefaultWindow, IDisposable
{
    private readonly WebViewControl _web;
    private readonly WebUiTuiIpc _ipc;

    private readonly Label _here = new()
    {
        Text = "бровзер: щоб обрати відео — відкрий YouTube або Twitch",
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

    /// <summary>The TV entity this browser drives; far away ⇒ auto-close.</summary>
    public EntityUid? TvUid;

    public WebTvPickerWindow()
    {
        Title = "Pirate TV — Браузер";
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
            MinWidth = 265,
            MaxWidth = 275,
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

        var s = WebTvWindow.Backend.Snapshot();
        _currentStatusLabel = new Label
        {
            Text = s.Kind == WebTvChannel.WebTvKind.None
                ? "Зараз на ТБ: (нічого не грає)"
                : "Зараз на ТБ: " + s.Label + " — " + s.Url,
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

        // Live mirror: lock-gates the buttons and refreshes the queue.
        WebTvWindow.Backend.Subscribe(OnPickerBroadcast);

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

        // Browser starts on YouTube's front page (whitelist keeps it inside).
        try { _web.Url = "https://www.youtube.com/?hl=uk"; } catch { /* headless dev */ }

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

        // Paint the current room (queue + now-playing + lock) right away,
        // not only after the next broadcast.
        OnPickerBroadcast(WebTvWindow.Backend.Snapshot());
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _ipc.Pump();

        // Walked away? The browser closes with the room (mirrors the TV).
        if (TvUid != null && TvUid.Value.IsValid())
        {
            try
            {
                var ent = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityManager>();
                var player = Robust.Shared.IoC.IoCManager
                    .Resolve<Robust.Client.Player.IPlayerManager>().LocalEntity;
                if (player != null &&
                    ent.TryGetComponent(player.Value, out Robust.Shared.GameObjects.TransformComponent? pt) &&
                    ent.TryGetComponent(TvUid.Value, out Robust.Shared.GameObjects.TransformComponent? tt) &&
                    pt.MapID == tt.MapID)
                {
                    var dv = pt.MapPosition.Position - tt.MapPosition.Position;
                    if (dv.Length() > 10.0)
                        Close();
                }
            }
            catch { }
        }

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
            var locked = WebTvWindow.Backend.Snapshot().Locked;
            _here.Text = $"{label}: ✓ готово до перегляду" + (locked ? " (ТБ заблоковано)" : "");
            _here.FontColorOverride = locked ? Color.Red : Color.LightGreen;
            _watch.Disabled = locked;
            _queueAdd.Disabled = locked;
            _pendingPlaybackUrl = playback;
            _pendingLabel = label;
            _pendingKind = kind;
        }
        else
        {
            _queueAdd.Disabled = true;
        }
    }

    // ===== pick / confirm / apply =====

    private void OnWatchPressed(BaseButton.ButtonEventArgs _)
    {
        PickPending();
    }

    private void OnConfirmYes(BaseButton.ButtonEventArgs _) => PerformConfirmYes();
    private void OnConfirmNo(BaseButton.ButtonEventArgs _) => PerformConfirmNo();
    private void OnQueueAddPressed(BaseButton.ButtonEventArgs _) => PerformQueueAdd();

    private void PickPending()
    {
        if (_pendingPlaybackUrl.Length == 0)
            return;

        var s = WebTvWindow.Backend.Snapshot();
        if (WebTvChannel.NeedsConfirm(s, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
        {
            // Someone is watching; replacement must be explicit.
            _confirmText.Text = "Зараз на ТБ грає: " + s.Label;
            _confirmPanel.Visible = true;
        }
        else
        {
            Apply();
        }
    }

    private void PerformConfirmYes()
    {
        Apply();
        _confirmPanel.Visible = false;
    }
    private void PerformConfirmNo()
    {
        // Keep browsing.
        _confirmPanel.Visible = false;
    }

    // ===== queue list + live lock gate (mirrors WebTvWindow) =====

    private void OnPickerBroadcast(WebTvBackend.ChannelState s)
    {
        if (_disposed)
            return;

        var locked = s.Locked;
        if (_watch != null && _queueAdd != null && _pendingPlaybackUrl.Length > 0)
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

        if (_pickerQueueTitle == null || _pickerQueueBox == null)
            return;
        _pickerQueueTitle.Text = "Черга (" + s.Queue.Count + ")" + (locked ? " [заблоковано]" : "");
        _pickerQueueBox.RemoveAllChildren();
        var idx = 0;
        foreach (var item in s.Queue)
        {
            var i = idx++;
            var title = item.Title.Length > 0 ? item.Title : item.Label;
            _pickerQueueBox.AddChild(WebTvWindow.BuildQueueRow(i, title, i == s.QueueNow, locked));
        }
    }

    /// <summary>Add the currently ready video to the room's playlist.</summary>
    private void PerformQueueAdd()    {
        if (_pendingPlaybackUrl.Length == 0)
            return;

        Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateTvQueueAddEvent
            {
                Url = _pendingPlaybackUrl,
                Kind = (int)_pendingKind,
                Label = _pendingLabel,
            });

        _here.Text = "➕ Додано в чергу";
        _here.FontColorOverride = Color.Orange;
    }

    /// <summary>Moves the room clock to the new channel: the server owns
    /// the state (TV-2) and its broadcast lands on every open TV window —
    /// in this client and everyone else's.</summary>
    private void Apply()
    {
        if (_pendingPlaybackUrl.Length == 0)
            return;

        Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateTvPickEvent
            {
                Url = _pendingPlaybackUrl,
                Kind = (int)_pendingKind,
                Label = _pendingLabel,
            });

        if (_currentStatusLabel != null)
            _currentStatusLabel.Text = "Зараз на ТБ: " + _pendingLabel + " — " + _pendingPlaybackUrl;
        _here.Text = "✔ Поставлено на ТБ";
        _here.FontColorOverride = Color.Orange;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
