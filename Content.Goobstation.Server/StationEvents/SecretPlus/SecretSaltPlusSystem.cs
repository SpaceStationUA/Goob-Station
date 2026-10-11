// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <ilyukarno@gmail.com>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Goobstation.Shared.StationEvents;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Roles;
using Content.Shared.Random.Helpers;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

/// <summary>
///   A scheduler which keeps track of a 'chaos score' which it tries to get closer to 0.
/// </summary>
[UsedImplicitly]
public sealed class SecretSaltPlusSystem : GameRuleSystem<SecretSaltPlusComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly EventManagerSystem _event = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    // cvars
    private float _minimumTimeUntilFirstEvent;
    private float _roundstartChaosScoreMultiplier;

    private ISawmill _sawmill = default!;

    // Reuse one client instead of opening a connection for every round.
    private static readonly HttpClient AuthorReportHttpClient = new();

    public override void Initialize()
    {
        base.Initialize();

        // The game ticker broadcasts rule starts after adding ActiveGameRuleComponent.
        // This distinguishes a rule being queued from one actually starting.
        SubscribeLocalEvent<GameRuleStartedEvent>(OnAnyGameRuleStarted);

        _sawmill = _log.GetSawmill("secret_salt_plus");

        Subs.CVar(_cfg, GoobCVars.MinimumTimeUntilFirstEvent, value => _minimumTimeUntilFirstEvent = value, true);
        Subs.CVar(_cfg, GoobCVars.RoundstartChaosScoreMultiplier, value => _roundstartChaosScoreMultiplier = value, true);
    }

    private void OnAnyGameRuleStarted(ref GameRuleStartedEvent args)
    {
        var query = EntityQueryEnumerator<SecretSaltPlusComponent>();
        while (query.MoveNext(out var schedulerUid, out var scheduler))
        {
            if (!scheduler.TelemetryRules.TryGetValue(args.RuleEntity, out var trackedRule))
                continue;

            scheduler.TelemetryStartedRules.Add(args.RuleEntity);
            LogMessage($"RULE STARTED scheduler={schedulerUid} candidate={trackedRule} ruleUid={args.RuleEntity}", false);
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, SecretSaltPlusComponent scheduler, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        var assignedMinds = 0;
        var assignedMindsUnique = new HashSet<EntityUid>();
        foreach (var (ruleUid, ruleId) in scheduler.TelemetryRules)
        {
            if (!Exists(ruleUid))
            {
                LogMessage($"RULE SUMMARY scheduler={uid} candidate={ruleId} ruleUid={ruleUid} state=missing", false);
                continue;
            }

            // AssignedMinds records assignments made by AntagSelectionSystem, not
            // necessarily every antagonist created by unrelated game systems.
            int? assigned = TryComp<AntagSelectionComponent>(ruleUid, out var antagSelection)
                ? antagSelection.AssignedMinds.Count
                : null;

            assignedMinds += assigned ?? 0;
            if (antagSelection != null)
            {
                foreach (var (mindUid, _) in antagSelection.AssignedMinds)
                    assignedMindsUnique.Add(mindUid);
            }

            var state = HasComp<ActiveGameRuleComponent>(ruleUid) ? "active"
                : HasComp<EndedGameRuleComponent>(ruleUid) ? "ended"
                : "pending";

            LogMessage($"RULE SUMMARY scheduler={uid} candidate={ruleId} ruleUid={ruleUid} everStarted={scheduler.TelemetryStartedRules.Contains(ruleUid)} state={state} assignedMinds={assigned?.ToString() ?? "n/a"}", false);
        }

        // Actual antagonist status at round end, including roles created outside
        // AntagSelectionSystem. Count only totals; never log player identifiers.
        var roundAntagonistMinds = 0;
        var roundAntagonistRoles = 0;
        var authorRoster = new List<string>();
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindUid, out var mind))
        {
            if (!_roles.MindIsAntagonist(mindUid))
                continue;

            var roles = _roles.MindGetAllRoleInfo(mindUid)
                .Where(role => role.Antagonist)
                .ToList();

            roundAntagonistMinds++;
            roundAntagonistRoles += roles.Count;

            // Follow the same name source as the vanilla round-end scoreboard.
            // Do not send NetUserId, mind UID, IP address, or other account data.
            var playerName = "Нікнейм невідомий";
            var userId = mind.UserId ?? mind.OriginalOwnerUserId;
            if (userId != null && _playerManager.TryGetPlayerData(userId.Value, out var playerData))
                playerName = playerData.ContentData()?.Name ?? playerName;

            var roleNames = roles.Select(role => SafeDiscordText(Loc.GetString(role.Name), 90)).ToList();
            var roleList = roleNames.Count > 0 ? string.Join(", ", roleNames) : "Невизначена роль";
            authorRoster.Add($"• {SafeDiscordText(playerName, 80)} — {roleList}");
        }

        scheduler.TelemetryAntagonistMindsAtEnd = roundAntagonistMinds;
        scheduler.TelemetryAntagonistRolesAtEnd = roundAntagonistRoles;
        scheduler.TelemetryUniqueTrackedAssignedMinds = assignedMindsUnique.Count;

        var skipReasons = string.Join(",", scheduler.TelemetryRoundstartSkips
            .OrderBy(pair => pair.Key)
            .Select(pair => $"{pair.Key}:{pair.Value}"));

        var summary = $"ROUND SUMMARY round={_ticker.RoundId} scheduler={uid} rulesAdded={scheduler.TelemetryRules.Count} rulesStarted={scheduler.TelemetryStartedRules.Count} assignedMinds={assignedMinds} trackedUniqueAssignedMinds={scheduler.TelemetryUniqueTrackedAssignedMinds} antagonistMindsAtEnd={scheduler.TelemetryAntagonistMindsAtEnd} antagonistRolesAtEnd={scheduler.TelemetryAntagonistRolesAtEnd} roundstartAttempts={scheduler.TelemetryRoundstartAttempts} roundstartAdded={scheduler.TelemetryRoundstartAdded} roundstartSkips=[{skipReasons}] initialPlayers={scheduler.TelemetryInitialPlayers} initialChaos={scheduler.TelemetryInitialChaos} chaos={scheduler.ChaosScore} midroundRolls={scheduler.TelemetryMidroundRolls} midroundCandidates={scheduler.TelemetryMidroundCandidates} midroundAdded={scheduler.TelemetryMidroundAdded} midroundNoCandidates={scheduler.TelemetryMidroundNoCandidates} duration={_ticker.RoundDuration()}";
        scheduler.TelemetryLastRoundSummary = summary;
        LogMessage(summary, false);

        // Only the private report contains nicknames and antagonist roles.
        // It is produced after the round ends, never inserted into the public
        // round-end scoreboard or administrative announcements.
        authorRoster.Sort(StringComparer.OrdinalIgnoreCase);
        var rosterText = authorRoster.Count == 0
            ? "Антагоністів наприкінці раунду не виявлено."
            : string.Join("\n", authorRoster);
        scheduler.TelemetryAuthorReport =
            $"🧂 Секрет Сіль 1.4 — раунд #{_ticker.RoundId}\n" +
            $"{summary}\n\nАнтагоністи наприкінці раунду (нікнейм — роль):\n{rosterText}";

        // Mandatory after every Secret Salt round; there is no on/off toggle.
        // The one-time server secret must point to the author's private channel.
        // Never fall back to the public station-report webhook.
        if (scheduler.TelemetryAuthorReportSendRequested)
            return;

        var privateWebhook = _cfg.GetCVar(GoobCVars.SecretSaltAuthorDiscordWebhook);
        if (string.IsNullOrWhiteSpace(privateWebhook))
        {
            // One warning per round rather than silently dropping the report.
            // The full private report remains available to the server until reset.
            _sawmill.Warning("Secret Salt author report NOT DELIVERED: mandatory private Discord webhook is not configured (secret_salt.author_discord_webhook).");
            return;
        }

        scheduler.TelemetryAuthorReportSendRequested = true;
        _ = SendAuthorReportAsync(privateWebhook, scheduler.TelemetryAuthorReport);
    }

    // Strip Discord formatting/control characters from player-controlled text,
    // and cap lengths to avoid oversized or forged report entries.
    private static string SafeDiscordText(string value, int limit)
    {
        var sanitized = new string(value
            .Where(c => !char.IsControl(c) && c != '`')
            .Take(limit)
            .ToArray());
        return sanitized.Replace("@", "＠").Replace("|", "¦");
    }

    private async Task SendAuthorReportAsync(string url, string report)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || (uri.Host != "discord.com" && uri.Host != "discordapp.com")
            || !uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal))
        {
            _sawmill.Warning("Secret Salt private report: invalid Discord webhook destination.");
            return;
        }

        try
        {
            // Discord has a 2000-character message limit; split at line boundaries.
            // This report is only sent after the scoreboard has revealed antagonists.
            var chunk = new StringBuilder();
            foreach (var rawLine in report.Split('\n'))
            {
                var line = rawLine.Length > 1700 ? rawLine[..1700] : rawLine;
                if (chunk.Length > 0 && chunk.Length + line.Length + 1 > 1900)
                {
                    await SendChunkAsync(uri, chunk.ToString());
                    chunk.Clear();
                }

                chunk.AppendLine(line);
            }

            if (chunk.Length > 0)
                await SendChunkAsync(uri, chunk.ToString());
        }
        catch (Exception)
        {
            // HTTP exception messages may contain a tokenized webhook URL.
            // Never log the URL, token, or individual player names.
            _sawmill.Warning("Secret Salt private author report delivery failed.");
        }
    }

    private static async Task SendChunkAsync(Uri destination, string message)
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = message,
            allowed_mentions = new { parse = Array.Empty<string>() }
        });

        // Retry rate limits and transient Discord errors. Avoid retrying permanent
        // failures such as an invalid or revoked webhook. Never log the URL.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await AuthorReportHttpClient.PostAsync(destination, content);
            if (response.IsSuccessStatusCode)
                return;

            var status = (int) response.StatusCode;
            var canRetry = status == 429 || status >= 500;
            if (!canRetry || attempt == 3)
            {
                response.EnsureSuccessStatusCode();
                return;
            }

            var delay = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(attempt * 2);
            await Task.Delay(delay);
        }
    }

    private static void RecordRoundstartSkip(SecretSaltPlusComponent scheduler, string reason)
    {
        scheduler.TelemetryRoundstartSkips.TryGetValue(reason, out var count);
        scheduler.TelemetryRoundstartSkips[reason] = count + 1;
    }

    protected override void Added(EntityUid uid, SecretSaltPlusComponent scheduler, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        var proto = MetaData(uid).EntityPrototype?.ID ?? "<unknown>";
        if (TryFindDuplicateScheduler(uid, proto, out var existingUid))
        {
            LogMessage($"DUPLICATE SKIP proto={proto} uid={uid} existingUid={existingUid}", false);
            ForceEndSelf(uid, gameRule);
            return;
        }

        var totalPlayers = GetTotalPlayerCount(_playerManager.Sessions);
        // set up starting chaos score
        scheduler.ChaosScore =
            -_random.NextFloat(scheduler.MinStartingChaos * totalPlayers, scheduler.MaxStartingChaos * totalPlayers) *
            _roundstartChaosScoreMultiplier;

        scheduler.TelemetryInitialPlayers = totalPlayers;
        scheduler.TelemetryInitialChaos = scheduler.ChaosScore;

        // roll midroundchaos generation variation
        var roll = _random.NextFloat();
        roll = MathF.Pow(roll, scheduler.ChaosChangeVariationExponent);
        // 50% chance to bias towards either higher chaos or lower chaos
        scheduler.ChaosChangeVariation = MathHelper.Lerp(1f,
                                                         _random.Prob(0.5f) ? scheduler.ChaosChangeVariationMin : scheduler.ChaosChangeVariationMax,
                                                         roll);
        LogMessage($"ADDED proto={proto} uid={uid} chaos={scheduler.ChaosScore} players={totalPlayers} multiplier={scheduler.ChaosChangeVariation}", false);
        LogMessage($"Using chaos change multiplier of {scheduler.ChaosChangeVariation}");

        TrySpawnRoundstartAntags((uid, scheduler)); // Roundstart antags need to be selected in the lobby
        if(TryComp<SelectedGameRulesComponent>(uid, out var selectedRules))
            SetupEvents((uid, scheduler), CountActivePlayers(), selectedRules);
        else
            SetupEvents((uid, scheduler), CountActivePlayers());
    }

    private bool TryFindDuplicateScheduler(EntityUid uid, string proto, out EntityUid existingUid)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var otherUid, out _, out var otherRule))
        {
            if (otherUid == uid || !GameTicker.IsGameRuleAdded(otherUid, otherRule))
                continue;

            if (MetaData(otherUid).EntityPrototype?.ID != proto)
                continue;

            existingUid = otherUid;
            return true;
        }

        existingUid = EntityUid.Invalid;
        return false;
    }

    /// <summary>
    ///   Build a list of events to use for the entire story
    /// </summary>
    private void SetupEvents(Entity<SecretSaltPlusComponent> scheduler, PlayerCount count, SelectedGameRulesComponent? selectedRules = null)
    {
        scheduler.Comp.SelectedEvents.Clear();

        if (selectedRules != null)
        {
            SelectFromTable(scheduler, count, selectedRules);
        }
        else
        {
            SelectFromAllEvents(scheduler, count);
        }
    }

    private void SelectFromAllEvents(Entity<SecretSaltPlusComponent> scheduler, PlayerCount count)
    {
        foreach (var proto in _ticker.GetAllGameRulePrototypes())
        {
            if (!proto.TryGetComponent<GameRuleComponent>(out var gameRule, _factory)
                || !proto.TryGetComponent<StationEventComponent>(out var stationEvent, _factory)
            )
                continue;

            if (scheduler.Comp.DisallowedEvents.Contains(stationEvent.EventType)
                || (!scheduler.Comp.IgnoreTimings
                    && !_event.CanRun(proto, stationEvent, count.Players, _ticker.RoundDuration(), 1f / GetRamping(scheduler)))
            )
                continue;

            scheduler.Comp.SelectedEvents.Add(new SelectedEvent(proto, gameRule, stationEvent));
        }
    }

    private void SelectFromTable(Entity<SecretSaltPlusComponent> scheduler, PlayerCount count, SelectedGameRulesComponent? selectedRules)
    {
        if (selectedRules == null)
            return;

        var available = _event.AvailableEvents(scheduler.Comp.IgnoreTimings,
                            scheduler.Comp.IgnoreTimings ? int.MaxValue : null,
                            scheduler.Comp.IgnoreTimings ? TimeSpan.MaxValue : null,
                            1f / GetRamping(scheduler));

        if (!_event.TryBuildLimitedEvents(selectedRules.ScheduledGameRules, available, out var possibleEvents))
            return;

        foreach (var entry in possibleEvents)
        {
            var proto = entry.Key;
            var stationEvent = entry.Value;
            if (!proto.TryGetComponent<GameRuleComponent>(out var gameRule, _factory))
                continue;

            if (scheduler.Comp.DisallowedEvents.Contains(stationEvent.EventType))
                continue;

            scheduler.Comp.SelectedEvents.Add(new SelectedEvent(proto, gameRule, stationEvent));
        }
    }

    /// <summary>
    ///   Decide what event to run next
    /// </summary>
    protected override void ActiveTick(EntityUid uid, SecretSaltPlusComponent scheduler, GameRuleComponent gameRule, float frameTime)
    {
        var count = CountActivePlayers();
        var ramp = GetRamping((uid, scheduler));
        var speedup = _event.EventSpeedup;
        var mult = scheduler.ChaosChangeVariation;

        scheduler.ChaosScore += count.Players * scheduler.LivingChaosChange * frameTime * ramp * speedup * mult;
        scheduler.ChaosScore += count.Ghosts * scheduler.DeadChaosChange * frameTime * speedup * mult;

        var currTime = _timing.CurTime;
        if (currTime < scheduler.TimeNextEvent)
            return;

        // This is the first event, add an automatic delay
        if (scheduler.TimeNextEvent == TimeSpan.Zero)
        {
            var time = _minimumTimeUntilFirstEvent / speedup;
            scheduler.TimeNextEvent = _timing.CurTime + TimeSpan.FromSeconds(time);
            LogMessage($"Started, first event in {time} seconds");
            return;
        }

        TimeSpan amt = TimeSpan.FromSeconds(_random.NextDouble(scheduler.EventIntervalMin.TotalSeconds, scheduler.EventIntervalMax.TotalSeconds) / ramp / speedup);
        scheduler.TimeNextEvent = currTime + amt;
                                                                          // generally more useful than curTime
        LogMessage($"Chaos score: {scheduler.ChaosScore}, Next event at: {_ticker.RoundDuration() + amt} (ramping {ramp})");

        if(TryComp<SelectedGameRulesComponent>(uid, out var selectedRules))
            SetupEvents((uid, scheduler), count, selectedRules);
        else
            SetupEvents((uid, scheduler), count);

        scheduler.TelemetryMidroundRolls++;
        var selectedEvent = ChooseEvent((uid, scheduler));
        if (selectedEvent != null)
        {
            scheduler.TelemetryMidroundCandidates++;
            var ruleUid = StartRule((uid, scheduler), selectedEvent.Proto.ID, false);
            if (ruleUid != EntityUid.Invalid)
            {
                scheduler.TelemetryMidroundAdded++;
                LogMessage($"MIDROUND ADDED scheduler={uid} candidate={selectedEvent.Proto.ID} ruleUid={ruleUid} chaos={scheduler.ChaosScore} players={count.Players} ghosts={count.Ghosts}", false);
            }
            else
            {
                LogMessage($"MIDROUND SKIP scheduler={uid} candidate={selectedEvent.Proto.ID} reason=nullChaosScoreAfterAdd", false);
            }
        }
        else
        {
            scheduler.TelemetryMidroundNoCandidates++;
            LogMessage($"MIDROUND SKIP scheduler={uid} reason=noRunnableEvents chaos={scheduler.ChaosScore} players={count.Players} ghosts={count.Ghosts}", false);
            LogMessage($"No runnable events");
        }

    }

    private ProtoId<TagPrototype> _loneSpawnTag = "LoneRunRule";

    /// <summary>
    /// Tries to spawn roundstart antags at the beginning of the round.
    /// </summary>
    private void TrySpawnRoundstartAntags(Entity<SecretSaltPlusComponent> scheduler)
    {
        if (scheduler.Comp.NoRoundstartAntags)
        {
            var skippedProto = MetaData(scheduler.Owner).EntityPrototype?.ID ?? "<unknown>";
            RecordRoundstartSkip(scheduler.Comp, "noRoundstartAntags");
            LogMessage($"ROUNDSTART SKIP proto={skippedProto} uid={scheduler.Owner} reason=noRoundstartAntags", false);
            return;
        }

        var primaryWeightList = _prototypeManager.Index(scheduler.Comp.PrimaryAntagsWeightTable);
        var weightList = _prototypeManager.Index(scheduler.Comp.RoundStartAntagsWeightTable);

        var count = GetTotalPlayerCount(_playerManager.Sessions);
        var proto = MetaData(scheduler.Owner).EntityPrototype?.ID ?? "<unknown>";

        LogMessage($"ROUNDSTART BEGIN proto={proto} uid={scheduler.Owner} chaos={scheduler.Comp.ChaosScore} players={count}", false);
        LogMessage($"Trying to run roundstart rules, total player count: {count}", false);

        var weights = weightList.Weights.ToDictionary();
        var primaryWeights = primaryWeightList.Weights.ToDictionary();
        int maxIters = 50, i = 0; // in case something dumb is tried
        var origChaos = scheduler.Comp.ChaosScore;
        while (scheduler.Comp.ChaosScore < 0 && i < maxIters)
        {
            i++;

            // on first iter pick a primary antag
            var pickWeights = i == 1 ? primaryWeights : weights;
            if (pickWeights.Count == 0 || pickWeights.Values.All(weight => weight <= 0f))
            {
                RecordRoundstartSkip(scheduler.Comp, "noPositiveWeights");
                LogMessage($"ROUNDSTART SKIP proto={proto} uid={scheduler.Owner} reason=noPositiveWeights iteration={i}", false);
                break;
            }

            var pick = _random.Pick(pickWeights);
            scheduler.Comp.TelemetryRoundstartAttempts++;

            // on lowpop this may still go no likey even for the primary antag pick and pick thief or something, intended
            GameRuleComponent? ruleComp = null;
            if (!_prototypeManager.TryIndex(pick, out var entProto)
                || !entProto.TryGetComponent<GameRuleComponent>(out ruleComp, _factory)
            )
            {
                RecordRoundstartSkip(scheduler.Comp, "missingGameRulePrototype");
                LogMessage($"ROUNDSTART SKIP proto={proto} uid={scheduler.Owner} candidate={pick} reason=missingGameRulePrototype", false);
                continue;
            }

            var chaosScore = GetChaosScore(entProto, ruleComp);

            if (chaosScore == null)
            {
                RecordRoundstartSkip(scheduler.Comp, "nullChaosScore");
                // This candidate is not eligible for chaos-based selection; the
                // structured skip below already records the reason. Integration tests
                // fail on ERROR logs, so do not treat a rejected candidate as a crash.
                LogMessage($"ROUNDSTART SKIP proto={proto} uid={scheduler.Owner} candidate={pick} reason=nullChaosScore", false);
                continue;
            }
                             // negative
            var pickProb = (-scheduler.Comp.ChaosScore) / chaosScore.Value;
            if (i == 1)
                pickProb *= scheduler.Comp.PrimaryAntagChaosBias;
            pickProb = MathF.Min(1f, pickProb); // to shut up debug
            if (!_random.Prob(pickProb)) // have a chance to re-pick if we have low chaos budget left compared to this
            {
                RecordRoundstartSkip(scheduler.Comp, "probability");
                LogMessage($"ROUNDSTART SKIP proto={proto} uid={scheduler.Owner} candidate={pick} reason=probability probability={pickProb} chaos={scheduler.Comp.ChaosScore} candidateScore={chaosScore.Value}", false);
                continue;
            }

            // for admeme presets
            if (!scheduler.Comp.IgnoreIncompatible)
            {
                weights.Remove(pick);

                if (_prototypeManager.TryIndex(pick, out IncompatibleGameModesPrototype? incompModes))
                    weights = weights.Where(w => !incompModes.Modes.Contains(w.Key)).ToDictionary();
            }

            IndexAndStartGameMode(pick, entProto, ruleComp);

            if (weights.Count == 0
                || (!scheduler.Comp.IgnoreIncompatible
                    && entProto.TryGetComponent<TagComponent>(out var tagComp, _factory)
                    && _tag.HasTag(tagComp, _loneSpawnTag)
                )
            )
            {
                LogMessage($"ROUNDSTART END proto={proto} uid={scheduler.Owner} chaos={scheduler.Comp.ChaosScore} iterations={i}", false);
                return;
            }
        }

        LogMessage($"ROUNDSTART END proto={proto} uid={scheduler.Owner} chaos={scheduler.Comp.ChaosScore} iterations={i}", false);
        return;

        void IndexAndStartGameMode(string pick, EntityPrototype? pickProto, GameRuleComponent? ruleComp)
        {
            if(pickProto == null
               || ruleComp == null
            )
                return;

            if (ruleComp.MinPlayers > count)
            {
                RecordRoundstartSkip(scheduler.Comp, "minPlayers");
                LogMessage($"ROUNDSTART SKIP proto={proto} uid={scheduler.Owner} candidate={pick} reason=minPlayers minPlayers={ruleComp.MinPlayers} players={count}", false);
                return;
            }

                                                // pick less antags if we have less chaos left
            var effPlayers = (int)MathF.Round(count * scheduler.Comp.ChaosScore / origChaos);
            var score = GetChaosScore(pickProto, ruleComp, effPlayers);
            var chaos = scheduler.Comp.ChaosScore;
            var ruleUid = StartRule(scheduler, pick, false, effPlayers);
            if (ruleUid == EntityUid.Invalid)
            {
                RecordRoundstartSkip(scheduler.Comp, "invalidChaosScoreAfterAdd");
                LogMessage($"ROUNDSTART SKIP proto={proto} uid={scheduler.Owner} candidate={pick} reason=invalidChaosScoreAfterAdd", false);
                return;
            }

            scheduler.Comp.TelemetryRoundstartAdded++;
            LogMessage($"ROUNDSTART ADDED proto={proto} uid={scheduler.Owner} candidate={pick} ruleUid={ruleUid} score={score} players={effPlayers} chaos={chaos}", false);
            LogMessage($"Roundstart rule chosen: {pick} with score {score}");
        }
    }

    /// <summary>
    ///   Adds and, optionally, starts a gamerule while respecting rules with variable chaos.
    /// </summary>
    private EntityUid StartRule(Entity<SecretSaltPlusComponent> scheduler, string rule, bool doStart = true, int? players = null)
    {
        var ruleUid = _ticker.AddGameRule(rule);

        // Automatically scheduled roundstart rules must not cancel the entire preset
        // if the number of available players changes after the initial selection.
        if (!doStart && TryComp<GameRuleComponent>(ruleUid, out var addedRule))
            addedRule.CancelPresetOnTooFewPlayers = false;

        var chaosScore = GetChaosScore(ruleUid, players);
        if (chaosScore is null)
        {
            // The rule was already added: mark it ended so it cannot start later.
            _ticker.EndGameRule(ruleUid);
            LogMessage($"RULE SKIP uid={ruleUid} candidate={rule} reason=nullChaosScoreAfterAdd", false);
            return EntityUid.Invalid;
        }

        scheduler.Comp.ChaosScore += chaosScore.Value;

        // if we hijack playercount, also hijack how many antags we pick
        if (players != null && TryComp<AntagSelectionComponent>(ruleUid, out var selection))
        {
            // i love C#
            for (var i = 0; i < selection.Definitions.Count; i++)
            {
                var def = selection.Definitions[i];
                def.Min = def.Max = _antagSelection.GetTargetAntagCount((ruleUid, selection), players, def);
                selection.Definitions[i] = def;
            }
        }

        // Keep the attribution before starting the rule: a start event can fire
        // synchronously, and it must be matched to this scheduler.
        scheduler.Comp.TelemetryRules[ruleUid] = rule;

        if (doStart)
            _ticker.StartGameRule(ruleUid);

        return ruleUid;
    }

    /// <summary>
    ///   Count the active players and ghosts on the server to determine how chaos changes.
    /// </summary>
    private PlayerCount CountActivePlayers()
    {
        var allPlayers = _playerManager.Sessions.ToList();
        var count = new PlayerCount();
        foreach (var player in allPlayers)
        {
            // TODO: A
            if (player.AttachedEntity != null)
            {
                // TODO: Consider a custom component here instead of HumanoidAppearanceComponent to represent
                //        "significant enough to count as a whole player"
                if (HasComp<HumanoidAppearanceComponent>(player.AttachedEntity))
                    count.Players += 1;
                // don't count bodyless ghosts aka say roundstart spectators
                // not reliable but works
                else if (TryComp<GhostComponent>(player.AttachedEntity, out var ghost) && ghost.CanReturnToBody)
                    count.Ghosts += 1;
            }
        }

        count.Players += _event.PlayerCountBias;

        return count;
    }

    public float? GetChaosScore(Entity<GameRuleComponent?> rule, int? players = null)
    {
        if (!Resolve(rule, ref rule.Comp))
            return null;

        if (TryComp<AntagSelectionComponent>(rule, out var selection))
        {
            var any = false;
            var score = 0f;

            foreach (var def in selection.Definitions)
            {
                if (def.ChaosScore == null)
                    continue;

                any = true;
                var count = _antagSelection.GetTargetAntagCount((rule, selection), players ?? GetTotalPlayerCount(_playerManager.Sessions), def);
                score += def.ChaosScore.Value * count;
            }

            if (any)
                return score;
        }

        return rule.Comp.ChaosScore;
    }

    public float? GetChaosScore(EntityPrototype ruleProto, GameRuleComponent? ruleComp, int? players = null)
    {
        if (ruleComp == null && !ruleProto.TryGetComponent<GameRuleComponent>(out ruleComp, _factory))
            return null;

        if (ruleProto.TryGetComponent<AntagSelectionComponent>(out var selection, _factory))
        {
            var any = false;
            var score = 0f;

            foreach (var def in selection.Definitions)
            {
                if (def.ChaosScore == null)
                    continue;

                any = true;
                var count = _antagSelection.GetTargetAntagCount((EntityUid.Invalid, selection), players ?? GetTotalPlayerCount(_playerManager.Sessions), def);
                score += def.ChaosScore.Value * count;
            }

            if (any)
                return score;
        }

        return ruleComp.ChaosScore;
    }

    /// <summary>
    ///   Count all the players on the server.
    /// </summary>
    public int GetTotalPlayerCount(IList<ICommonSession> pool)
    {
        return _antagSelection.GetTotalPlayerCount(pool) + _event.PlayerCountBias;
    }

    public float GetRamping(Entity<SecretSaltPlusComponent> scheduler)
    {
        var curTime = _ticker.RoundDuration();
        return 1f + (float)curTime.TotalSeconds * scheduler.Comp.SpeedRamping * _event.EventSpeedup;
    }

    /// <summary>
    ///   Picks an event based on current chaos score, events' chaos scores and weights.
    /// </summary>
    private SelectedEvent? ChooseEvent(Entity<SecretSaltPlusComponent> scheduler)
    {
        var possible = scheduler.Comp.SelectedEvents;
        Dictionary<SelectedEvent, float> weights = new();

        foreach (var ev in possible)
        {
            if (ev.EvComp == null)
                continue;

            var chaosScore = GetChaosScore(ev.Proto, ev.RuleComp);
            if (chaosScore == null)
            {
                Log.Error($"Tried running event {ev.Proto.ID}, but chaos score was null");
                continue;
            }

            var weight = chaosScore.Value;
            bool negative = weight < 0f;
            weight = MathF.Abs(weight);
            weight = MathF.Pow(weight, scheduler.Comp.ChaosExponent);
            if (negative) weight = -weight;
            weight += scheduler.Comp.ChaosOffset; // offset negative-chaos events upwards too else they never happen
            weight += weight < 0f ? -scheduler.Comp.ChaosThreshold : scheduler.Comp.ChaosThreshold; // make sure it's not in (-1, 1) to not get absurdly low event probabilities
            var delta = ChaosDelta(-scheduler.Comp.ChaosScore, weight, scheduler.Comp.ChaosMatching, scheduler.Comp.ChaosThreshold * scheduler.Comp.ChaosThreshold);
            weights[ev] = ev.EvComp.Weight / (delta + 1f);
        }

        return weights.Count == 0 ? null : _random.Pick(weights);
    }

    private float ChaosDelta(float chaos1, float chaos2, float logBase, float differentSignMultiplier)
    {
        float ratio = chaos2 / chaos1;
        if (ratio < 0f) ratio = MathF.Abs(chaos2 * chaos1 / differentSignMultiplier);
        return MathF.Abs(MathF.Log(ratio, logBase));
    }

    private void LogMessage(string message, bool showChat=true)
    {
        // TODO: LogMessage strings all require localization.
        _adminLogger.Add(LogType.SecretPlus, showChat?LogImpact.Medium:LogImpact.High, $"{message}");
        if (showChat)
            _chat.SendAdminAnnouncement("SecretPlus " + message);

    }
}
