// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.WebView;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     UI-scale watcher for embedded CEF pages. The engine paints a
///     browser's bitmap sized to the scale factor CEF captured at browser
///     creation and blits it raw, so when the user changes their UI scale
///     an embedded page keeps its stale size. The fix stays content-side:
///     subscribe to display.uiScale and have each host REBUILD its
///     webview (fresh browser -> fresh mapping) after a scale change.
/// </summary>
public static class PirateWebViewScaleWatcher
{
    public static float Current => IoCManager.Resolve<IConfigurationManager>().GetCVar(CVars.DisplayUIScale);

    /// <summary>Call once per system. onChange fires every cvar event
    /// with the new scale; the caller is usually debounced by its own
    /// FrameUpdate tick.</summary>
    public static void Watch(Action<float> onChange)
    {
        IoCManager.Resolve<IConfigurationManager>()
            .OnValueChanged(CVars.DisplayUIScale, onChange, false);
    }
}
