// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.Maths;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     The galactic chart, in a window.
///
/// ## This is the first slice, and it is deliberately thin
///
///     It is a window, a navigation fence, and a URL. There is no bridge and no state:
///     the page renders from its own committed bake and nothing crosses between C# and
///     the page yet. That is a deliberate order rather than a half-finished feature.
///
///     The reason is that the renderer has never been inside a <c>DefaultWindow</c>, and
///     the browser it was tuned in is not the viewport it will run in. Every visual
///     decision in this chart was made against a 1600x1000 browser at
///     <c>TUI_IFACE=GalaxyMap npm run dev</c>; a 1120x660 CEF window with different
///     DPI, different font metrics and a different compositor is a different rendering
///     target. Finding out what that looks like is worth more than the bridge, and the
///     bridge cannot be designed sensibly until the surface it pushes state into is
///     known to exist at the right size.
///
/// ## The fence is not optional
///
///     Every non-<c>res://</c> navigation is cancelled. Without it a click on the chart's
///     own background can walk the CEF browser off to the open web, and a CEF window in
///     a game client is a browser with no address bar -- so there would be no way back and
///     nothing on screen saying where it went. This is the same fence
///     <c>WebUiTuiIpc</c> applies for the TV and the arcade.
///
/// ## Where the page comes from
///
///     <c>Resources/_Pirate/WebUI/GalaxyMap/</c>, which is COMMITTED output, exactly like
///     <c>Radio/</c>, <c>ThemePicker/</c> and <c>EvidenceBoard/</c>. It is produced by
///     <c>TUI_IFACE=GalaxyMap npm run build</c> in
///     <c>Content.Pirate.Client/_Pirate/WebUI/Ui</c> and copied over; there is no build
///     step that does this automatically, so a rebuild that is not copied leaves the
///     committed bundle stale and nothing will say so.
///
///     The chart also reads <c>orionSpur.yml</c> at BUILD time, not at runtime: the bake
///     turns the hand-drawn intent polygons into a committed cell list
///     (<c>lib/baked.ts</c>, with a fingerprint) and that is what ships. So a change to a
///     border needs <c>npm run bake</c> and a rebuild, not just a page reload.
/// </summary>
public sealed class WebGalaxyMapWindow : DefaultWindow, IDisposable
{
    /**
     *     The committed bundle.
     *
     *     "res://webres/_Pirate/..." and NOT "res://_Pirate/...", and the difference is
     *     the whole reason the first attempt showed a page reading "Not found" in a
     *     perfectly working browser.
     *
     *     The stock upstream Web module resolves the content prefix INSIDE the path
     *     rather than re-attaching a res:// host per content root. Every other webui
     *     window here already knows this -- `WebArcadeWindow.ResPrefix` is
     *     "res://webres/" and `WebThemeWindow` has its own -- and the local engine
     *     patch's own comment spells it out. This file was written from the PLAN.md
     *     description of the ORIGINAL spike, which predates that change, so it carried
     *     the old scheme.
     *
     *     Borrowed from `WebArcadeWindow.ResPrefix` rather than spelled out again, so
     *     there is one constant for it.
     */
    public const string PageUrl = WebArcadeWindow.ResPrefix + "_Pirate/WebUI/GalaxyMap/index.html";

    private readonly WebViewControl _web;
    private readonly Label _status = new()
    {
        Text = "",
        FontColorOverride = Color.Gray,
        ClipText = true,
    };

    private bool _disposed;

    public WebGalaxyMapWindow()
    {
        // 1120x660 matches WebTvWindow. Chosen for the ratio rather than the size: the
        // chart is a landscape hex field and the overlay panel is sized in absolute
        // pixels, so the interesting variable is how much WIDTH it gets.
        Title = "Galactic Chart — Orion Spur";
        SetSize = new Vector2i(1120, 660);

        _web = new WebViewControl
        {
            // The chart animates continuously (the disc turns, the knots travel), so the
            // browser must keep running when the window loses focus. Without this a
            // player who clicks away sees the chart frozen and cannot tell whether it
            // has stopped or merely stopped being visible.
            AlwaysActive = true,
        };

        // The fence. Cancels everything that is not our own bundle.
        //
        // ONE argument, not two, and cancelling is `ctx.DoCancel()` rather than a bool
        // return. That signature cost a build: the handler is an
        // Action<IBeforeBrowseContext>, and a lambda written as (control, url) => bool
        // is a compile error rather than a wrong answer at runtime. WebUiTuiIpc's
        // HandleBeforeBrowse is the reference for the shape.
        _web.AddBeforeBrowseHandler(ctx =>
        {
            try
            {
                if (ctx.Url.StartsWith(WebArcadeWindow.ResPrefix, StringComparison.Ordinal))
                    return;
                ctx.DoCancel();
            }
            catch
            {
                // A URL we cannot even read is not our page.
                try { ctx.DoCancel(); } catch { /* already gone */ }
            }
        });

        _web.VerticalExpand = true;
        _web.HorizontalExpand = true;

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _status.Text =
            "bundle: Resources/_Pirate/WebUI/GalaxyMap/ — re-run " +
            "TUI_IFACE=GalaxyMap npm run build and copy dist_resources/ over it";
        column.AddChild(_status);

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(new Color(20, 26, 34)),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(_web);
        column.AddChild(panel);

        Contents.AddChild(column);

        // AlwaysActive keeps the CEF browser alive past the window, so it has to be
        // released explicitly or it keeps a live renderer and a GL context running for a
        // chart nobody is looking at.
        OnClose += () =>
        {
            try { _web.AlwaysActive = false; } catch { /* headless dev */ }
        };

        try
        {
            _web.Url = PageUrl;
        }
        catch (Exception e)
        {
            _status.Text = "could not open the chart: " + e.Message;
            _status.FontColorOverride = Color.Red;
        }
    }

    /// <summary>
    ///     Opens the chart, or focuses it if it is already up.
    ///
    ///     One window at a time, deliberately. The chart is a browser and each one is a
    ///     WebGL context with live animation; a player who can open six of them has six
    ///     contexts and a frame rate problem, and nothing about the chart wants to be
    ///     open twice.
    /// </summary>
    private static WebGalaxyMapWindow? _open;

    public static void Open()
    {
        if (_open != null)
        {
            _open.OpenCentered();
            _open.MoveToFront();
            return;
        }

        var window = new WebGalaxyMapWindow();
        _open = window;
        window.OpenCentered();
        window.OnClose += () => _open = null;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}