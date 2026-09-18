// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Dev commands for the Pirate TV.
///       webuitv        — opens a viewer window watching the room channel.
///       webuitvpick    — opens the picker browser (YouTube/Twitch fence).
///       webuitv <url>  — "owner" override: sets a room channel directly.
///     Multiple windows share the static <see cref="WebTvWindow.Backend"/>,
///     which demos the sync loop locally (TV-2 replaces it with the server
///     relay); the flow (pick → confirm → switch) stays identical.
/// </summary>
[AnyCommand]
public sealed class WebTvCommand : IConsoleCommand
{
    public string Command => "webuitv";

    public string Description => "Opens a Pirate TV window watching the room channel.";

    public string Help =>
        "Usage: webuitv [url]\n  No url → opens a viewer window on the current room channel.\n" +
        "  With url (YouTube video / Twitch channel or VOD) → sets it as the room channel directly.\n" +
        "  'webuitvpick' opens the picker browser instead.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var window = new WebTvWindow();
        window.OpenCenteredTv();

        if (args.Length == 1)
        {
            var url = args[0];
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                shell.WriteLine("Only http(s) URLs are supported in TV mode.");
                return;
            }
            if (!WebTvChannel.TryBuild(url, out var kind, out var playback, out var label))
            {
                shell.WriteLine("Not a YouTube video or Twitch channel/VOD page.");
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            WebTvWindow.Backend.Apply(s =>
            {
                s.Url = playback;
                s.Label = label;
                s.Kind = kind;
                s.Playing = true;
                s.Pos = 0;
                s.Stamp = now;
            });
        }
    }
}

/// <summary>Opens the TV browser window (YouTube/Twitch fence) for picking a channel.</summary>
[AnyCommand]
public sealed class WebTvPickCommand : IConsoleCommand
{
    public string Command => "webuitvpick";

    public string Description => "Opens the Pirate TV picker browser (YouTube/Twitch).";

    public string Help => "Usage: webuitvpick";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var window = new WebTvPickerWindow();
        window.OpenCenteredPicker();
    }
}
