using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Common.StationReport;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Robust.Shared.Configuration;

namespace Content.Goobstation.Server.StationReportDiscordIntergrationSystem;

public sealed partial class StationReportDiscordIntergration : EntitySystem
{
    //thank you Timfa for writing this code
    private static readonly HttpClient client = new();
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    private string? _webhookUrl;
    private int? _lastReportRound;

    public override void Initialize()
    {
        base.Initialize();

        //subscribes to the endroundevent and Stationreportevent
        SubscribeLocalEvent<StationReportEvent>(OnStationReportReceived);

        // Keep track of CCVar value, update if changed
        _cfg.OnValueChanged(GoobCVars.StationReportDiscordWebHook, url => _webhookUrl = url, true);
    }

    public static string? report;

    private static readonly TagReplacement[] _replacements =
    {
        new(@"\[/?bold\]", @"**"),
        new(@"\[/?italic\]", @"_"),
        new(@"\[/?mono\]", @"__"),
        new(@">", @""),
        new(@"\[h1\]", @"# "),
        new(@"\[h2\]", @"## "),
        new(@"\[h3\]", @"### "),
        new(@"\[h4\]", @"-# "),
        new(@"\[/h[0-9]\]", @""),
        new(@"\[head=1\]", @"# "),
        new(@"\[head=2\]", @"## "),
        new(@"\[head=3\]", @"### "),
        new(@"\[head=4\]", @"-# "),
        new(@"\[/head\]", @""),
        new(@"\[/?color(=[#0-9a-zA-Z]+)?\]", @"")
    };

    private void OnStationReportReceived(StationReportEvent ev)
    {
        report = ev.StationReportText;

        // Pirate: an admin can reopen the scoreboard and raise this event again.
        if (_ticker.RunLevel != GameRunLevel.PostRound || _lastReportRound == _ticker.RoundId ||
            string.IsNullOrWhiteSpace(report) || string.IsNullOrWhiteSpace(_webhookUrl))
            return;

        foreach (var replacement in _replacements)
            report = Regex.Replace(report, replacement.Tag, replacement.Replacement);

        _lastReportRound = _ticker.RoundId;
        _ = SendMessageAsync(_webhookUrl, report, _ticker.RoundId);
    }

    private async Task SendMessageAsync(string url, string message, int roundId)
    {
        try
        {
            using var content = ReportContent(message, roundId);
            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                if ((int) response.StatusCode is >= 400 and < 500 && _lastReportRound == roundId)
                    _lastReportRound = null;
                Log.Error($"Discord rejected station report for round {roundId}: {response.StatusCode}");
            }
        }
        catch (Exception e)
        {
            // Delivery is unknown on a transport error. A repeated scoreboard must not resend it.
            Log.Error($"Could not confirm Discord delivery of station report for round {roundId}: {e.GetType().Name}");
        }
    }

    // Pirate: keep the whole paper in one Discord message and disable mentions in every format.
    private static HttpContent ReportContent(string message, int roundId)
    {
        var mentions = new WebhookMentions();
        if (message.Length <= 2000)
            return JsonContent.Create(new { content = message, allowed_mentions = mentions });

        if (message.Length <= 4096)
            return JsonContent.Create(new { embeds = new[] { new { description = message } }, allowed_mentions = mentions });

        var form = new MultipartFormDataContent();
        form.Add(JsonContent.Create(new { allowed_mentions = mentions }), "payload_json");
        form.Add(new StringContent(message, Encoding.UTF8, "text/plain"), "files[0]", $"station-report-{roundId}.txt");
        return form;
    }

    public struct TagReplacement
    {
        public string Tag, Replacement;
        public TagReplacement(string tag, string replacement)
        {
            Tag = tag;
            Replacement = replacement;
        }
    }
}
