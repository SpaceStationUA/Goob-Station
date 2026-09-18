// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Parsing for what a TV "channel" is — a YouTube video or a Twitch
///     stream page. Whitelisted picker traffic is exactly these hosts, so
///     the parse list doubles as the license posture: only officially
///     embeddable sources, each client keeps its own session.
/// </summary>
public static class WebTvChannel
{
    public enum WebTvKind { None, YouTube, Twitch }

    /// <summary>Single ground-truth host list (pickers and TVs share it).</summary>
    public static readonly string[] AllowHosts =
    {
        // YouTube: embed page, watch/shorts pages, media + static CDNs, consent.
        "youtube.com",
        "*.youtube.com",
        "youtu.be",
        "youtube-nocookie.com",
        "*.youtube-nocookie.com",
        "googlevideo.com",
        "*.googlevideo.com",
        "ytimg.com",
        "*.ytimg.com",
        "ggpht.com",
        "*.ggpht.com",
        "google.com",
        "*.google.com",
        "gstatic.com",
        "*.gstatic.com",

        // Twitch: site, embedded player shell, media/graphic CDNs.
        "twitch.tv",
        "*.twitch.tv",
        "twitchcdn.net",
        "*.twitchcdn.net",
        "jtvnw.net",
        "*.jtvnw.net",
    };

    /// <summary>
    ///     Turns a browsed URL into what the TV should actually play.
    ///     Returns false for anything Uri-but-not-a-video (search pages,
    ///     listings, playlists, clips).
    /// </summary>
    public static bool TryBuild(string browsedUrl, out WebTvKind kind, out string playbackUrl, out string label)
    {
        kind = WebTvKind.None;
        playbackUrl = "";
        label = "";

        // Plain ctor (no UriKind — it is not in the sandbox whitelist).
        var uri = new Uri(browsedUrl);
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return false;

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath;

        // ----- YouTube -----
        if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtu.be")
        {
            var id = host == "youtu.be"
                ? FirstSegment(path)
                : TryQuery(uri, "v") ?? PrefixId(path, "/embed/") ?? PrefixId(path, "/shorts/") ?? PrefixId(path, "/live/");

            if (string.IsNullOrWhiteSpace(id) || id.Length is < 5 or > 20)
                return false;

            kind = WebTvKind.YouTube;
            // Top-level /embed pages answer with YT error 153 in this CEF;
            // the plain watch page's HTML5 player plays directly.
            playbackUrl = "https://www.youtube.com/watch?v=" + id + "&hl=uk";
            label = "YouTube";
            return true;
        }

        // ----- Twitch -----
        if (host is "twitch.tv" or "www.twitch.tv" or "m.twitch.tv" or "player.twitch.tv")
        {
            var trimmed = path.TrimEnd('/');

            // Embedded player form: ?channel=name / ?video=123456.
            if (TryQuery(uri, "channel") is { Length: > 0 } chq)
            {
                kind = WebTvKind.Twitch;
                playbackUrl = "https://www.twitch.tv/" + chq;
                label = "Twitch (live)";
                return true;
            }
            if (TryQuery(uri, "video") is { Length: > 0 } vidq && vidq.Length <= 12)
            {
                kind = WebTvKind.Twitch;
                playbackUrl = "https://www.twitch.tv/videos/" + vidq;
                label = "Twitch VOD";
                return true;
            }

            // Site VOD: /videos/123456.
            var vIdx = trimmed.IndexOf("/videos/", StringComparison.Ordinal);
            if (vIdx >= 0 && FirstSegment(trimmed[(vIdx + "/videos/".Length)..]) is { Length: > 4 } vid)
            {
                kind = WebTvKind.Twitch;
                playbackUrl = "https://www.twitch.tv/videos/" + vid;
                label = "Twitch VOD";
                return true;
            }

            // Live channel page: a single non-tab segment (/name).
            if (trimmed.Length > 1 && !trimmed[1..].Contains('/') &&
                trimmed[1..] is not ("videos" or "directory" or "downloads" or "p" or "settings" or "about" or "terms" or "privacy"))
            {
                kind = WebTvKind.Twitch;
                playbackUrl = "https://www.twitch.tv" + trimmed;
                label = "Twitch (live)";
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    ///     True when the shared state holds a recently picked channel that
    ///     is still on screen somewhere (drives the picker's confirm step).
    /// </summary>
    public static bool NeedsConfirm(WebTvBackend.ChannelState s, long nowMs)
    {
        return s.Kind != WebTvKind.None
               && s.Url.Length > 0
               && nowMs - s.Stamp < 15 * 60 * 1000;
    }

    // ===== tiny URL helpers =====

    private static string? TryQuery(Uri uri, string key)
    {
        var q = uri.Query;
        if (q.Length < 2)
            return null;
        foreach (var pair in q[1..].Split('&'))
        {
            var eq = pair.IndexOf('=');
            var k = eq < 0 ? pair : pair[..eq];
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase) && eq >= 0)
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return null;
    }

    private static string? PrefixId(string path, string prefix)
    {
        var idx = path.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var rest = path[(idx + prefix.Length)..];
        return FirstSegment(rest) is { Length: >= 5 } id ? id : null;
    }

    /// <summary>Returns the segment up to the next '/' (or the end).</summary>
    private static string? FirstSegment(string tail)
    {
        if (tail.StartsWith('/'))
            tail = tail[1..];
        var end = tail.IndexOf('/');
        var seg = end < 0 ? tail : tail[..end];
        if (seg.Length == 0)
            return null;
        foreach (var ch in seg)
            if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_')
                return null;
        return seg;
    }
}
