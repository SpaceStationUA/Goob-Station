// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Pirate.Shared.Radio;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Network;
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
    [Dependency] private readonly INetManager _net = default!;

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
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _http.Dispose();
    }

    private void OnCatalogRequest(PirateRadioCatalogRequestEvent msg, EntitySessionEventArgs args)
    {
        var marker = GetEntity(msg.Marker);
        if (!Exists(marker))
            return;

        EnsureCache();

        var stations = Pinned();
        if (_cache != null)
            stations.AddRange(_cache);

        RaiseNetworkEvent(new PirateRadioCatalogEvent
        {
            Marker = msg.Marker,
            Stations = stations,
            FromNetwork = _cache != null,
        }, args.SenderSession.Channel);
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
                state.Label = station.Label;
                state.Url = station.Url;
                state.Playing = true;
                state.Stamp = Now();
                break;
            }
            case "stop":
                state.Playing = false;
                break;
            default:
                return;
        }

        RaiseNetworkEvent(new PirateRadioStateEvent
        {
            Marker = msg.Marker,
            StationId = state.StationId,
            Label = state.Label,
            Url = state.Url,
            Playing = state.Playing,
            Stamp = state.Stamp,
        }, session.Channel);
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

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private sealed class RadioSession
    {
        public string StationId = "";
        public string Label = "";
        public string Url = "";
        public bool Playing;
        public long Stamp;
    }
}
