// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.ServerStatus;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    [Dependency] private readonly PlayTimeTrackingManager _careerTimes = default!;
    [Dependency] private readonly IServerPreferencesManager _careerPreferences = default!;

    private sealed record CareerRequest(Guid Player, string Job);

    private async Task PirateJobs(IStatusHandlerContext context)
    {
        var departments = await RunOnMainThread(() => _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .Select(dept => new
            {
                id = dept.ID,
                name = Loc.GetString(dept.Name),
                jobs = dept.Roles.Select(id => _prototypeManager.Index(id)).Where(job => job.SetPreference)
                    .Select(job => new { id = job.ID, name = job.LocalizedName }).OrderBy(job => job.name).ToArray(),
            }).Where(dept => dept.jobs.Length > 0).OrderBy(dept => dept.name).ToArray());
        await context.RespondJsonAsync(new { departments });
    }

    private async Task PirateJobProgress(IStatusHandlerContext context)
    {
        var request = await ReadJson<CareerRequest>(context);
        if (request == null)
            return;
        if (request.Player == Guid.Empty || string.IsNullOrEmpty(request.Job) || request.Job.Length > 100)
        {
            await context.RespondJsonAsync(new { error = "invalid_request" }, HttpStatusCode.BadRequest);
            return;
        }
        var user = new NetUserId(request.Player);
        var result = await RunOnMainThread<object?>(() =>
        {
            if (!_playerManager.TryGetSessionById(user, out var session) || !_careerTimes.TryGetTrackerTimes(session, out var times)
                || !_careerPreferences.TryGetCachedPreferences(user, out var prefs))
                return null;
            return CareerResult(request.Job, times, prefs);
        });
        if (result == null)
        {
            var timers = await _moderationDb.GetPlayTimes(request.Player);
            var prefs = await _moderationDb.GetPlayerPreferencesAsync(user, CancellationToken.None);
            var times = timers.ToDictionary(timer => timer.Tracker, timer => timer.TimeSpent);
            result = await RunOnMainThread(() => CareerResult(request.Job, times, prefs));
        }
        await context.RespondJsonAsync(result);
    }

    private object CareerResult(string jobId, IReadOnlyDictionary<string, TimeSpan> times, PlayerPreferences? prefs)
    {
        if (!_prototypeManager.TryIndex<JobPrototype>(jobId, out var job) || !job.SetPreference)
            return new { error = "unknown_job" };
        HumanoidCharacterProfile? profile = null;
        if (prefs != null && prefs.Characters.TryGetValue(prefs.SelectedCharacterIndex, out var selected))
            profile = selected as HumanoidCharacterProfile;
        var enabled = _config.GetCVar(CCVars.GameRoleTimers);
        var rows = new List<object>();
        var requirements = _entityManager.System<SharedRoleSystem>().GetRoleRequirements(job);
        if (enabled && requirements != null)
        {
            foreach (var rule in requirements)
            {
                var met = rule.Check(_entityManager, _prototypeManager, profile, times, out var reason);
                string label;
                TimeSpan current;
                TimeSpan required;
                switch (rule)
                {
                    case RoleTimeRequirement role:
                        var roleJob = _entityManager.System<Content.Shared.Roles.Jobs.SharedJobSystem>().GetJobPrototype(role.Role);
                        label = _prototypeManager.TryIndex<JobPrototype>(roleJob, out var tracked) ? tracked.LocalizedName : role.Role.ToString();
                        current = times.GetValueOrDefault(role.Role.Id);
                        required = role.Time;
                        break;
                    case DepartmentTimeRequirement dept:
                        var department = _prototypeManager.Index(dept.Department);
                        label = Loc.GetString(department.Name);
                        current = TimeSpan.FromTicks(department.Roles.Sum(id => times.GetValueOrDefault(_prototypeManager.Index(id).PlayTimeTracker).Ticks));
                        required = dept.Time;
                        break;
                    case OverallPlaytimeRequirement overall:
                        label = Loc.GetString("pirate-career-overall");
                        current = times.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall);
                        required = overall.Time;
                        break;
                    default:
                        label = rule switch
                        {
                            AgeRequirement => Loc.GetString("pirate-career-age"),
                            SpeciesRequirement => Loc.GetString("pirate-career-species"),
                            TraitsRequirement => Loc.GetString("pirate-career-traits"),
                            _ => Loc.GetString("pirate-career-character"),
                        };
                        rows.Add(new { label, met = profile == null ? (bool?) null : met,
                            detail = !met ? reason?.ToString() : null });
                        continue;
                }
                rows.Add(new { label, met, current = current.TotalSeconds, required = required.TotalSeconds, upper = rule.Inverted });
            }
        }
        return new { job = job.LocalizedName, character = profile?.Name, enabled, requirements = rows, whitelist = job.Whitelisted };
    }
}
