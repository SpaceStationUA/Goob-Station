using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared._Pirate.CCVar;
using Content.Shared._Pirate.UkraineAlarm;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.UkraineAlarm;

public sealed class UkraineAlarmSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IHttpClientHolder _http = default!;

    private readonly Dictionary<NetUserId, string> _playerRegions = new();
    private readonly Dictionary<string, UkraineAlarmDangerLevel> _regionStates = new();
    private readonly Dictionary<string, string> _regionParents = new();
    private readonly Dictionary<string, HashSet<string>> _regionChildren = new();
    private readonly List<UkraineAlarmRegion> _regions = new();
    private readonly SemaphoreSlim _apiRequestSemaphore = new(1, 1);

    private static readonly TimeSpan AlertUpdateInterval = TimeSpan.FromMinutes(1);

    private TimeSpan _nextAlertUpdate;
    private bool _updatingAlerts;
    private bool _loadedRegions;
    private bool _injectBilaTserkvaAlarm;
    private Task? _regionsLoadTask;

    public override void Initialize()
    {
        base.Initialize();
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        _ = EnsureRegionsLoadedAsync();
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _apiRequestSemaphore.Dispose();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (!_updatingAlerts && now >= _nextAlertUpdate)
        {
            _nextAlertUpdate = now + AlertUpdateInterval;
            _ = UpdateAlarmsAsync();
        }
    }

    public async void SetPlayerRegion(ICommonSession session, string regionId)
    {
        if (!_regions.Any(region => region.Id == regionId))
            return;

        _playerRegions[session.UserId] = regionId;
        await _db.SetUkraineAlarmRegionAsync(session.UserId, regionId);
        NotifySessionOfCurrentState(session, regionId);
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Connected)
            return;

        await EnsureRegionsLoadedAsync();

        var regionId = await _db.GetUkraineAlarmRegionAsync(args.Session.UserId);
        if (regionId == null)
        {
            OpenRegionSelect(args.Session);
            return;
        }

        _playerRegions[args.Session.UserId] = regionId;
        NotifySessionOfCurrentState(args.Session, regionId);
    }

    private void OpenRegionSelect(ICommonSession session)
    {
        if (_regions.Count == 0)
            return;

        var eui = new UkraineAlarmRegionSelectEui(this, _regions.ToArray());
        _eui.OpenEui(eui, session);
        eui.StateDirty();
    }

    private Task EnsureRegionsLoadedAsync()
    {
        if (_regionsLoadTask != null)
            return _regionsLoadTask;

        _regionsLoadTask = LoadRegionsAsync();
        return _regionsLoadTask;
    }

    private async Task LoadRegionsAsync()
    {
        var token = NormalizeToken(_cfg.GetCVar(PirateCVars.UkraineAlarmApiToken));
        if (string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var json = await GetApiJsonAsync("https://api.ukrainealarm.com/api/v3/regions", token);
            var parsed = ParseRegions(json);

            _regions.Clear();
            _regionParents.Clear();
            _regionChildren.Clear();
            var addedRegionIds = new HashSet<string>();
            foreach (var region in parsed)
            {
                if (string.IsNullOrWhiteSpace(region.RegionId) || string.IsNullOrWhiteSpace(region.RegionName))
                    continue;

                if (addedRegionIds.Add(region.RegionId))
                    _regions.Add(new UkraineAlarmRegion(region.RegionId, region.RegionName));

                if (string.IsNullOrWhiteSpace(region.ParentRegionId))
                    continue;

                _regionParents[region.RegionId] = region.ParentRegionId;
                if (!_regionChildren.TryGetValue(region.ParentRegionId, out var children))
                {
                    children = new HashSet<string>();
                    _regionChildren[region.ParentRegionId] = children;
                }

                children.Add(region.RegionId);
            }

            _loadedRegions = _regions.Count > 0;
            if (_loadedRegions)
                _nextAlertUpdate = _timing.CurTime;

            Log.Info($"Завантажено районів України для повітряних тривог: {_regions.Count}");
        }
        catch (UkraineAlarmRequestException e)
        {
            Log.Warning($"UkraineAlarm API request failed:\n{e}");
        }
        catch (Exception e)
        {
            Log.Error($"Не вдалося завантажити райони України для повітряних тривог: {e}");
        }
    }

    private async Task UpdateAlarmsAsync()
    {
        if (_updatingAlerts)
            return;

        _updatingAlerts = true;
        try
        {
            if (!_loadedRegions)
                return;

            var token = NormalizeToken(_cfg.GetCVar(PirateCVars.UkraineAlarmApiToken));
            if (string.IsNullOrWhiteSpace(token))
                return;

            var states = await GetAlarmStatesAsync(token);
            foreach (var (regionId, level) in states)
            {
                _regionStates.TryGetValue(regionId, out var previous);
                if (previous == level)
                    continue;

                _regionStates[regionId] = level;
                NotifyRegion(regionId, level);
            }
        }
        catch (UkraineAlarmRequestException e)
        {
            _nextAlertUpdate = _timing.CurTime + AlertUpdateInterval;

            Log.Warning(
                $"UkraineAlarm API request failed:\n{e}");
        }
        catch (Exception e)
        {
            Log.Error($"Не вдалося оновити повітряні тривоги України: {e}");
        }
        finally
        {
            _updatingAlerts = false;
        }
    }
    private async Task<Dictionary<string, UkraineAlarmDangerLevel>> GetAlarmStatesAsync(string token)
    {
        var json = await GetApiJsonAsync("https://api.ukrainealarm.com/api/v3/alerts", token);
        var alerts = JsonSerializer.Deserialize<List<ApiAlert>>(json, JsonOptions()) ?? [];
        var states = _regions.ToDictionary(region => region.Id, _ => UkraineAlarmDangerLevel.None);

        foreach (var alert in alerts)
        {
            foreach (var activeAlert in alert.ActiveAlerts ?? [])
            {
                var regionId = activeAlert.RegionId ?? alert.RegionId ?? alert.Region?.RegionId;
                if (string.IsNullOrWhiteSpace(regionId))
                    continue;

                ApplyAlarmState(states, regionId, ParseDangerLevel(activeAlert));
            }

            if (alert.ActiveAlerts is { Count: > 0 })
                continue;

            var fallbackRegionId = alert.RegionId ?? alert.Region?.RegionId;
            if (string.IsNullOrWhiteSpace(fallbackRegionId))
                continue;

            ApplyAlarmState(states, fallbackRegionId, ParseDangerLevel(alert));
        }

        if (_injectBilaTserkvaAlarm)
        {
            _injectBilaTserkvaAlarm = false;
            var regionId = FindBilaTserkvaDistrictId();
            if (regionId == null)
            {
                Log.Warning("Не вдалося знайти Білоцерківський район для тестової повітряної тривоги.");
            }
            else
            {
                ApplyAlarmState(states, regionId, UkraineAlarmDangerLevel.Red);
                Log.Info($"До відповіді /alerts одноразово додано тестову тривогу для Білоцерківського району ({regionId}).");
            }
        }

        return states;
    }

    public bool QueueBilaTserkvaTestAlarm()
    {
        var regionId = FindBilaTserkvaDistrictId();
        if (regionId == null)
            return false;

        _injectBilaTserkvaAlarm = true;
        _regionStates.Remove(regionId);
        return true;
    }

    private string? FindBilaTserkvaDistrictId()
    {
        foreach (var region in _regions)
        {
            if (!region.Name.Contains("район", StringComparison.OrdinalIgnoreCase))
                continue;

            if (region.Name.Contains("Білоцерків", StringComparison.OrdinalIgnoreCase) ||
                region.Name.Contains("Белоцерков", StringComparison.OrdinalIgnoreCase) ||
                region.Name.Contains("Bila Tserkva", StringComparison.OrdinalIgnoreCase))
            {
                return region.Id;
            }
        }

        return null;
    }

    private void ApplyAlarmState(
        Dictionary<string, UkraineAlarmDangerLevel> states,
        string regionId,
        UkraineAlarmDangerLevel level)
    {
        SetMaxDangerLevel(states, regionId, level);

        var visited = new HashSet<string> { regionId };
        var ancestor = regionId;
        while (_regionParents.TryGetValue(ancestor, out var parent) && visited.Add(parent))
        {
            SetMaxDangerLevel(states, parent, level);
            ancestor = parent;
        }

        var pending = new Stack<string>();
        pending.Push(regionId);
        while (pending.TryPop(out var current))
        {
            if (!_regionChildren.TryGetValue(current, out var children))
                continue;

            foreach (var child in children)
            {
                if (!visited.Add(child))
                    continue;

                SetMaxDangerLevel(states, child, level);
                pending.Push(child);
            }
        }
    }

    private static void SetMaxDangerLevel(
        Dictionary<string, UkraineAlarmDangerLevel> states,
        string regionId,
        UkraineAlarmDangerLevel level)
    {
        states[regionId] = MaxDangerLevel(states.GetValueOrDefault(regionId), level);
    }

    private static string NormalizeToken(string token)
    {
        token = token.Trim().Trim('"', '\'');

        const string authorizationPrefix = "Authorization:";
        if (token.StartsWith(authorizationPrefix, StringComparison.OrdinalIgnoreCase))
            token = token[authorizationPrefix.Length..].Trim();

        return token;
    }

    private static List<ApiRegion> ParseRegions(string json)
    {
        var regions = new List<ApiRegion>();
        using var document = JsonDocument.Parse(json);
        CollectRegions(document.RootElement, regions, null);
        return regions;
    }

    private static void CollectRegions(JsonElement element, List<ApiRegion> regions, string? parentRegionId)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectRegions(child, regions, parentRegionId);
                break;
            case JsonValueKind.Object:
                var childParentRegionId = parentRegionId;
                if (TryReadRegion(element, out var region))
                {
                    region.ParentRegionId = parentRegionId;
                    regions.Add(region);
                    childParentRegionId = region.RegionId;
                }

                foreach (var property in element.EnumerateObject())
                    CollectRegions(property.Value, regions, childParentRegionId);
                break;
        }
    }

    private static bool TryReadRegion(JsonElement element, out ApiRegion region)
    {
        region = new ApiRegion();

        if (!element.TryGetProperty("regionId", out var idProperty) || idProperty.ValueKind != JsonValueKind.String)
            return false;

        if (!element.TryGetProperty("regionName", out var nameProperty) || nameProperty.ValueKind != JsonValueKind.String)
            return false;

        region.RegionId = idProperty.GetString();
        region.RegionName = nameProperty.GetString();
        return !string.IsNullOrWhiteSpace(region.RegionId) && !string.IsNullOrWhiteSpace(region.RegionName);
    }

    private async Task<string> GetApiJsonAsync(string url, string token)
    {
        token = NormalizeToken(token);

        await _apiRequestSemaphore.WaitAsync();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", token);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await _http.Client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new UkraineAlarmRequestException(
                    $"UkraineAlarm API returned HTTP {(int) response.StatusCode} ({response.ReasonPhrase}) for {url}.");
            }

            return responseBody;
        }
        catch (HttpRequestException e)
        {
            throw new UkraineAlarmRequestException($"UkraineAlarm API request failed for {url}.", e);
        }
        finally
        {
            _apiRequestSemaphore.Release();
        }
    }

    private void NotifyRegion(string regionId, UkraineAlarmDangerLevel level)
    {
        foreach (var session in _players.Sessions)
        {
            if (!_playerRegions.TryGetValue(session.UserId, out var selectedRegion) || selectedRegion != regionId)
                continue;

            RaiseNotification(session, level);
        }
    }

    private void NotifySessionOfCurrentState(ICommonSession session, string regionId)
    {
        if (!_regionStates.TryGetValue(regionId, out var level) || level == UkraineAlarmDangerLevel.None)
            return;

        RaiseNotification(session, level);
    }

    private void RaiseNotification(ICommonSession session, UkraineAlarmDangerLevel level)
    {
        var text = level switch
        {
            UkraineAlarmDangerLevel.Red or UkraineAlarmDangerLevel.Yellow =>
                "У вашому районі почалася повітряна тривога.\nСтежте за офіційними повідомленнями.",
            _ => "Повітряну тривогу у вашому районі скасовано.",
        };

        RaiseNetworkEvent(new UkraineAlarmNotificationMessage(text, level), session);
    }

    private static UkraineAlarmDangerLevel ParseDangerLevel(ApiAlert alert)
    {
        var level = alert.AlertType ?? alert.Type ?? alert.AlertLevel ?? alert.Level ?? string.Empty;
        return ParseDangerLevel(level, alert.ActiveAlertLevels);
    }

    private static UkraineAlarmDangerLevel ParseDangerLevel(ApiActiveAlert alert)
    {
        return ParseDangerLevel(alert.Type ?? string.Empty, alert.ActiveAlertLevels);
    }

    private static UkraineAlarmDangerLevel ParseDangerLevel(string level, List<ApiAlertLevel>? activeAlertLevels)
    {
        foreach (var activeLevel in activeAlertLevels ?? [])
        {
            if (string.Equals(activeLevel.AlertLevel, "Red", StringComparison.OrdinalIgnoreCase))
                return UkraineAlarmDangerLevel.Red;
        }

        foreach (var activeLevel in activeAlertLevels ?? [])
        {
            if (string.Equals(activeLevel.AlertLevel, "Yellow", StringComparison.OrdinalIgnoreCase))
                return UkraineAlarmDangerLevel.Yellow;
        }

        if (level.Contains("red", StringComparison.OrdinalIgnoreCase) || level.Contains("critical", StringComparison.OrdinalIgnoreCase))
            return UkraineAlarmDangerLevel.Red;

        return UkraineAlarmDangerLevel.Yellow;
    }

    private static UkraineAlarmDangerLevel MaxDangerLevel(UkraineAlarmDangerLevel current, UkraineAlarmDangerLevel next)
    {
        return next > current ? next : current;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private sealed class UkraineAlarmRequestException : Exception
    {
        public UkraineAlarmRequestException(string message) : base(message)
        {
        }

        public UkraineAlarmRequestException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    private sealed class ApiRegion
    {
        public string? RegionId { get; set; }
        public string? RegionName { get; set; }
        public string? ParentRegionId { get; set; }
    }

    private sealed class ApiAlert
    {
        public string? RegionId { get; set; }
        public string? AlertType { get; set; }
        public string? Type { get; set; }
        public string? AlertLevel { get; set; }
        public string? Level { get; set; }
        public ApiRegion? Region { get; set; }
        public List<ApiActiveAlert>? ActiveAlerts { get; set; }
        public List<ApiAlertLevel>? ActiveAlertLevels { get; set; }
    }

    private sealed class ApiActiveAlert
    {
        public string? RegionId { get; set; }
        public string? Type { get; set; }
        public List<ApiAlertLevel>? ActiveAlertLevels { get; set; }
    }

    private sealed class ApiAlertLevel
    {
        public string? AlertLevel { get; set; }
    }
}























