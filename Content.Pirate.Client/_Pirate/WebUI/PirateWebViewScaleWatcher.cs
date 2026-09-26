// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     UI-scale handling for embedded CEF pages: watch display.uiScale,
///     and after each host rebuild a per-view layout nudge forces the
///     engine to re-allocate the CEF compositor texture (without it the
///     view can paint blank after a rebuild; clicks still worked, only
///     the raster never refreshed).
/// </summary>
public static class PirateWebViewScaleWatcher
{
    public static float Current => IoCManager.Resolve<IConfigurationManager>().GetCVar(CVars.DisplayUIScale);

    /// <summary>Call once per system. onChange fires every cvar event with
    /// the new scale; the caller is usually debounced by its own
    /// FrameUpdate tick.</summary>
    public static void Watch(Action<float> onChange)
    {
        IoCManager.Resolve<IConfigurationManager>()
            .OnValueChanged(CVars.DisplayUIScale, onChange, false);
    }
}

/// <summary>Post-rebuild layout nudger shared by every CEF host.</summary>
public sealed class PirateWebViewNudger
{
    private readonly List<(Control View, int Ticks)> _nudges = new();
    private readonly List<(Control View, int T)> _nudgesDown = new();

    /// <summary>Queue a +frame/−frame margin pulse; debris-free even if
    /// the same view is nudged twice in a row (a rebuild flushes the old).</summary>
    public void Queue(Control view)
    {
        // Skip duplicates: last queued entry always wins.
        _nudges.RemoveAll(n => n.View == view);
        _nudgesDown.RemoveAll(n => n.View == view);
        _nudges.Add((view, 3)); // three frames up, then pulse down
    }

    public void Tick()
    {
        for (var i = _nudges.Count - 1; i >= 0; i--)
        {
            var (view, t) = _nudges[i];
            if (view.Disposed) { _nudges.RemoveAt(i); continue; }
            if (t == 0)
            {
                // Pulse: +1px, then restore after two more frames.
                _nudges.RemoveAt(i);
                view.Margin = new Robust.Shared.Maths.Thickness(1f);
                _nudgesDown.Add((view, 3));
            }
            else
                _nudges[i] = (view, t - 1);
        }
        for (var i = _nudgesDown.Count - 1; i >= 0; i--)
        {
            var (view, t) = _nudgesDown[i];
            if (view.Disposed) { _nudgesDown.RemoveAt(i); continue; }
            if (t == 0)
            {
                _nudgesDown.RemoveAt(i);
                view.Margin = new Robust.Shared.Maths.Thickness(0f);
            }
            else
                _nudgesDown[i] = (view, t - 1);
        }
    }
}
