// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Pirate.WebUi;
using Content.Pirate.Shared.Radio;
using Content.Shared._Pirate.CCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Radio;

/// <summary>
///     Serves the internet-radio station catalog for the PDA radio program.
///     The curated, pinned prototypes always come first and work offline;
///     when the network allows, radio-browser.info is queried for additional
///     Ogg-only stations and merged in. Results are cached with a TTL.
/// </summary>
public sealed class PirateRadioSystem : EntitySystem
{
    private const string ApiUserAgent = "GoobStation-Radio/0.1 (+https://goobstation.com)";
    private static readonly string[] Mirrors =
    {
        "https://de1.api.radio-browser.info",
        "https://de2.api.radio-browser.info",
        "https://fi1.api.radio-browser.info",
        "https://nl1.api.radio-browser.info",
    };

    private const int MaxRemote = 40;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HttpClient _http = new() { Timeout = FetchTimeout };
    private readonly Dictionary<NetEntity, RadioSession> _sessions = new();

    private List<PirateRadioStationEntry>? _cache;
    private DateTimeOffset _cacheAt;
    private bool _fetching;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateRadioCatalogRequestEvent>(OnCatalogRequest);
        SubscribeNetworkEvent<PirateRadioCommandEvent>(OnCommand);
        SubscribeNetworkEvent<PirateRadioRelayReadyEvent>(OnRelayReady);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var marker in new List<NetEntity>(_pumps.Keys))
            KillPump(marker, notify: false);
        _http.Dispose();
    }

    /// <summary>Drain pump buffers onto the network; watchdog for stalls.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pumps.Count == 0)
            return;

        List<NetEntity>? dead = null;
        foreach (var (marker, pump) in _pumps)
        {
            // Stalled pump (dead upstream, dead ffmpeg): kill it and tell
            // the page the session is over.
            if (DateTimeOffset.UtcNow - pump.LastData > RelayStallTimeout)
            {
                Log.Warning($"Radio relay stalled for {marker}, killing pump.");
                dead ??= new List<NetEntity>();
                dead.Add(marker);
                continue;
            }

            if (pump.Proc.HasExited && pump.PendingBytes == 0)
            {
                // Stream ended naturally (and fully flushed): end the session.
                dead ??= new List<NetEntity>();
                dead.Add(marker);
                continue;
            }

            if (!pump.Ready)
            {
                // The page never reported ready (pda closed mid-start): keep
                // a bounded backlog so a later join still works.
                TrimRelayBacklog(pump);
                continue;
            }

            var chunk = TakeRelayChunk(pump);
            if (chunk != null)
                RaiseNetworkEvent(new PirateRadioRelayChunkEvent
                {
                    Marker = marker,
                    Data = chunk,
                }, pump.Channel);
        }

        if (dead != null)
        {
            foreach (var marker in dead)
                KillPump(marker, notify: true);
        }
    }

    private void OnCatalogRequest(PirateRadioCatalogRequestEvent msg, EntitySessionEventArgs args)
    {
        var marker = GetEntity(msg.Marker);
        if (!Exists(marker))
            return;

        if (_cfg.GetCVar(PirateVars.RadioRemoteCatalog))
            EnsureCache();
        else
            _cache = null; // cvar turned off: fall back to pinned-only

        RaiseNetworkEvent(new PirateRadioCatalogEvent
        {
            Marker = msg.Marker,
            Stations = StationsFor(),
            Theme = ThemeOf(marker),
            Themes = AllowedThemes(marker),
        }, args.SenderSession.Channel);
    }

    
    private string ThemeOf(EntityUid marker)
        => WebUi.PirateWebThemeResolver.ThemeOf(_entMan, _prototypes, marker);

    private List<string> AllowedThemes(EntityUid marker)
        => WebUi.PirateWebThemeResolver.AllowedThemes(_entMan, _prototypes, marker);

    /// <summary>Re-push fresh catalogs (theme included) for every session
    /// whose marker sits under this PDA. Called after a theme switch.</summary>
    public void RepushCatalogsForPda(EntityUid pda, INetChannel channel)
    {
        List<NetEntity> dead = new();
        var pushed = 0;
        foreach (var (marker, session) in _sessions)
        {
            if (!Exists(GetEntity(marker)))
                continue;
            var parent = _entMan.TryGetComponent<TransformComponent>(GetEntity(marker), out var t) ? t.ParentUid : EntityUid.Invalid;
            if (!parent.IsValid() || parent != pda)
                continue;

            RaiseNetworkEvent(new PirateRadioCatalogEvent
            {
                Marker = marker,
                Stations = StationsFor(),
                Theme = ThemeOf(GetEntity(marker)),
                Themes = AllowedThemes(GetEntity(marker)),
            }, channel);
            pushed++;
        }
        Logger.DebugS("webui.radio", $"repushed {pushed} radio catalog(s) for pda {pda}");
    }

    /// <summary>The full station list (pins + remote cache per cvar).</summary>
    private List<PirateRadioStationEntry> StationsFor()
    {
        var stations = Pinned();
        if (_cache != null)
            stations.AddRange(_cache);
        return stations;
    }

    private void OnCommand(PirateRadioCommandEvent msg, EntitySessionEventArgs args)
    {
        var marker = GetEntity(msg.Marker);
        if (!Exists(marker))
            return;

        var session = args.SenderSession;
        if (!_sessions.TryGetValue(msg.Marker, out var state))
            state = _sessions[msg.Marker] = new RadioSession();

        switch (msg.Op)
        {
            case "play":
            {
                var station = Find(msg.StationId);
                if (station == null)
                    return;
                state.StationId = station.Id;
                state.Playing = true;
                state.Relay = station.Relay;
                if (station.Relay)
                {
                    StartRelay(msg.Marker, station, session.Channel);
                }
                break;
            }
            case "stop":
                state.Playing = false;
                state.Relay = false;
                KillPump(msg.Marker, notify: false);
                break;
            case "theme":
            {
                // Both PDA Settings and per-app switches land here. The
                // component override lives on the PDA (the cartridge's
                // parent); the catalog's theme field flows to every app
                // through the normal re-push path.
                if (string.IsNullOrEmpty(msg.Theme))
                    return;
                if (!AllowedThemes(marker).Contains(msg.Theme))
                {
                    Logger.DebugS("webui.radio", $"theme switch to {msg.Theme} rejected (not allowed for this device)");
                    return;
                }
                var pda = _entMan.GetComponent<TransformComponent>(marker).ParentUid;
                if (!pda.IsValid())
                    return;
                var themeEnt = CompOrNull<PirateWebUiThemeComponent>(pda);
                if (themeEnt == null)
                    themeEnt = EnsureComp<PirateWebUiThemeComponent>(pda);
                themeEnt.WebThemeId = msg.Theme;
                Dirty(pda, themeEnt);
                Logger.DebugS("webui.radio", $"theme switch on {marker} -> {msg.Theme}");
                var stations = Pinned();
                if (_cache != null)
                    stations.AddRange(_cache);
                RaiseNetworkEvent(new PirateRadioCatalogEvent
                {
                    Marker = msg.Marker,
                    Stations = stations,
                    Theme = msg.Theme,
                    Themes = AllowedThemes(marker),
                }, session.Channel);
                break;
            }
            default:
                return;
        }

        RaiseNetworkEvent(new PirateRadioStateEvent
        {
            Marker = msg.Marker,
            StationId = state.StationId,
            Playing = state.Playing,
            Relay = state.Relay,
        }, session.Channel);
    }

    private void OnRelayReady(PirateRadioRelayReadyEvent msg, EntitySessionEventArgs args)
    {
        if (!_pumps.TryGetValue(msg.Marker, out var pump))
            return;

        // The re-opened program fragment re-registers as the relay listener.
        pump.Channel = args.SenderSession.Channel;
        pump.Ready = true;
        pump.Kick = true; // flush the init segment regardless of size
    }

    private void StartRelay(NetEntity marker, PirateRadioStationEntry station, INetChannel channel)
    {
        var ffmpeg = _cfg.GetCVar(PirateVars.RadioFfmpegPath);
        if (string.IsNullOrWhiteSpace(ffmpeg))
        {
            // Transcoding disabled: the page will fail to decode this stream
            // on its own, which the picker treats as an unavailable station.
            return;
        }

        if (_pumps.TryGetValue(marker, out var existing))
        {
            if (existing.Url == station.Url && !existing.Proc.HasExited)
                return; // same station already pumping; keep it
            KillPump(marker, notify: false);
        }

        // file: inputs (testing) play at realtime; live http/https throttles
        // themselves. Quoting is safe: we exec without a shell.
        var inputArgs = station.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? "-re "
            : "";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = "-hide_banner -loglevel error " + inputArgs +
                "-i \"" + station.Url + "\" -map 0:a -c:a libopus -b:a 48k -f webm pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process proc;
        try
        {
            proc = Process.Start(psi)!;
        }
        catch (Exception e)
        {
            Log.Warning($"Radio relay failed to start ffmpeg: {e.Message}");
            return;
        }

        _pumps[marker] = new RelayPump
        {
            Proc = proc,
            Channel = channel,
            Url = station.Url,
        };
        var pump = _pumps[marker];
        _ = Task.Run(() => PumpStdout(pump, proc));
        Log.Debug($"Radio relay started for {marker}: {station.Url}");
    }

    private async Task PumpStdout(RelayPump pump, Process proc)
    {
        var buffer = new byte[8192];
        try
        {
            var stdout = proc.StandardOutput.BaseStream;
            while (true)
            {
                var n = await stdout.ReadAsync(buffer, pump.Cts.Token);
                if (n <= 0)
                    break;
                var chunk = new byte[n];
                Buffer.BlockCopy(buffer, 0, chunk, 0, n);
                lock (pump.Lock)
                {
                    pump.Pending.Add(chunk);
                    pump.PendingBytes += n;
                }
                pump.LastData = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Warning($"Radio pump read ended: {e.Message}");
        }
    }

    private const int RelayBacklogCap = 256 * 1024;

    private void TrimRelayBacklog(RelayPump pump)
    {
        lock (pump.Lock)
        {
            while (pump.PendingBytes > RelayBacklogCap && pump.Pending.Count > 0)
            {
                var first = pump.Pending[0];
                pump.Pending.RemoveAt(0);
                pump.PendingBytes -= first.Length;
            }
        }
    }

    /// <summary>Join the next uploadable chunk (merged up to the target).</summary>
    private byte[]? TakeRelayChunk(RelayPump pump)
    {
        lock (pump.Lock)
        {
            if (pump.Pending.Count == 0)
                return null;
            if (!pump.Kick && pump.PendingBytes < RelayChunkTarget)
                return null;

            var parts = new List<byte[]>();
            var size = 0;
            while (pump.Pending.Count > 0 && size < RelayChunkTarget)
            {
                var next = pump.Pending[0];
                if (size + next.Length > RelayChunkTarget && size > 0)
                    break;
                pump.Pending.RemoveAt(0);
                parts.Add(next);
                size += next.Length;
            }
            pump.PendingBytes -= size;
            pump.Kick = false;

            var merged = new byte[size];
            var offset = 0;
            foreach (var p in parts)
            {
                Buffer.BlockCopy(p, 0, merged, offset, p.Length);
                offset += p.Length;
            }
            return merged;
        }
    }

    private void KillPump(NetEntity marker, bool notify)
    {
        if (!_pumps.Remove(marker, out var pump))
            return;

        try { pump.Cts.Cancel(); } catch { /* already dead */ }
        try { pump.Proc.Kill(entireProcessTree: true); } catch { /* already dead */ }

        if (notify)
        {
            try
            {
                RaiseNetworkEvent(new PirateRadioStateEvent
                {
                    Marker = marker,
                    Playing = false,
                }, pump.Channel);
            }
            catch
            {
                /* channel already gone */
            }
        }
    }

    /// <summary>Curated stations from prototypes, in declaration order.</summary>
    private List<PirateRadioStationEntry> Pinned()
    {
        var list = new List<PirateRadioStationEntry>();
        foreach (var proto in _prototypes.EnumeratePrototypes<PirateRadioStationPrototype>())
        {
            list.Add(new PirateRadioStationEntry
            {
                Id = proto.ID,
                Label = proto.Label,
                Genre = proto.Genre,
                Url = proto.Url,
                Featured = proto.Featured,
                Relay = proto.Transcode,
            });
        }
        return list;
    }

    private PirateRadioStationEntry? Find(string id)
    {
        foreach (var pinned in Pinned())
        {
            if (pinned.Id == id)
                return pinned;
        }
        if (_cache == null)
            return null;
        foreach (var entry in _cache)
        {
            if (entry.Id == id)
                return entry;
        }
        return null;
    }

    private void EnsureCache()
    {
        if (_cache != null && DateTimeOffset.UtcNow - _cacheAt < CacheTtl)
            return;
        if (_fetching)
            return;
        _fetching = true;
        FetchRemote();
    }

    /// <summary>
    ///     Fire-and-forget remote fetch, mirroring Content.Server's Discord
    ///     webhook pattern (async void + callback), so the sim never blocks.
    /// </summary>
    private async void FetchRemote()
    {
        try
        {
            foreach (var mirror in Mirrors)
            {
                var json = await TryFetch(mirror);
                if (json == null)
                    continue;
                var parsed = Parse(json);
                if (parsed.Count == 0)
                    continue;
                // The community catalog is full of dead mounts and mp3/aac
                // streams our CEF cannot decode. Offer only stations whose
                // URL answers right now and starts with real Ogg data.
                parsed = await VerifyAsync(parsed);
                if (parsed.Count == 0)
                    continue;
                _cache = parsed;
                _cacheAt = DateTimeOffset.UtcNow;
                return;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Radio catalog fetch failed: {e.Message}");
        }
        finally
        {
            _fetching = false;
        }
    }

    /// <summary>Keep only stations whose stream answers with Ogg magic bytes.</summary>
    private async Task<List<PirateRadioStationEntry>> VerifyAsync(List<PirateRadioStationEntry> input)
    {
        var results = new List<PirateRadioStationEntry>();
        var tasks = new List<Task<PirateRadioStationEntry?>>();
        using var gate = new SemaphoreSlim(8);
        foreach (var station in input)
        {
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync();
                try
                {
                    return await IsPlayable(station.Url) ? station : null;
                }
                finally
                {
                    gate.Release();
                }
            }));
        }
        var done = await Task.WhenAll(tasks);
        foreach (var s in done)
        {
            if (s != null)
                results.Add(s);
        }
        return results;
    }

    private async Task<bool> IsPlayable(string url)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(ApiUserAgent);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!resp.IsSuccessStatusCode)
                return false;
            var media = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Contains("html", StringComparison.OrdinalIgnoreCase))
                return false;
            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            var header = new byte[4];
            var read = 0;
            while (read < header.Length)
            {
                var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read), cts.Token);
                if (n <= 0)
                    break;
                read += n;
            }
            return read == 4
                && header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S';
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> TryFetch(string mirror)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{mirror}/json/stations/search?codec=OGG&hidebroken=true&order=clickcount&reverse=true&limit={MaxRemote}");
            req.Headers.UserAgent.ParseAdd(ApiUserAgent);
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return null;
            return await resp.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
    }

    private List<PirateRadioStationEntry> Parse(string json)
    {
        var list = new List<PirateRadioStationEntry>();
        try
        {
            var doc = JsonDocument.Parse(json);
            foreach (var s in doc.RootElement.EnumerateArray())
            {
                var url = s.TryGetProperty("url_resolved", out var u) ? u.GetString()
                    : s.TryGetProperty("url", out var u2) ? u2.GetString() : null;
                var name = s.TryGetProperty("name", out var n) ? n.GetString() : null;
                var uuid = s.TryGetProperty("stationuuid", out var id) ? id.GetString() : null;
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name))
                    continue;
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    continue;

                var genre = "";
                if (s.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.String)
                    genre = tags.GetString() ?? "";

                list.Add(new PirateRadioStationEntry
                {
                    Id = "rb:" + (uuid ?? name),
                    Label = name.Trim(),
                    Genre = genre,
                    Url = url,
                });
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Radio catalog parse failed: {e.Message}");
        }
        return list;
    }

private sealed class RadioSession
{
    public string StationId = "";
    public string Label = "";
    public bool Playing;
    public bool Relay;
}

private sealed class RelayPump
{
    public readonly object Lock = new();
    public readonly List<byte[]> Pending = new();
    public int PendingBytes;
    public readonly CancellationTokenSource Cts = new();
    public Process Proc = null!;
    public INetChannel Channel = null!;
    public string Url = "";
    public bool Ready;
    public bool Kick;
    public DateTimeOffset LastData = DateTimeOffset.UtcNow;
}

private const int RelayChunkTarget = 16000;
private static readonly TimeSpan RelayStallTimeout = TimeSpan.FromSeconds(20);

// Media elements on a secure res:// origin cannot reach an http:// relay
// endpoint, and the client CEF has no MP3/AAC decoders at all. For such
// stations the server fetches the upstream stream, transcodes to WebM/Opus
// with ffmpeg and pushes the bytes through the game connection; the page
// plays them with a MediaSource. One ffmpeg process per active session.
private readonly Dictionary<NetEntity, RelayPump> _pumps = new();

}
