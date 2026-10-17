// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Roles;

public sealed class SpeciesJobWhitelistSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    // Source identity detects requirement override changes without explicit invalidation.
    private readonly Dictionary<string, (HashSet<JobRequirement>? Source, HashSet<JobRequirement> Result)> _cache = new();

    // Defer lookup until prototypes have loaded.
    private bool? _anyWhitelist;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SpeciesJobWhitelistPrototype>() || args.WasModified<JobPrototype>())
            Refresh();
    }

    private void Refresh()
    {
        _cache.Clear();
        _anyWhitelist = null;
    }

    public HashSet<JobRequirement>? AddRequirement(JobPrototype job, HashSet<JobRequirement>? requirements)
    {
        _anyWhitelist ??= _prototypes.Count<SpeciesJobWhitelistPrototype>() > 0;
        if (!_anyWhitelist.Value || !job.SetPreference)
            return requirements;

        if (_cache.TryGetValue(job.ID, out var cached) && ReferenceEquals(cached.Source, requirements))
            return cached.Result;

        // Requirement sets are shared; copy before adding the species gate.
        var result = requirements == null
            ? new HashSet<JobRequirement>()
            : new HashSet<JobRequirement>(requirements);
        result.Add(new SpeciesJobWhitelistRequirement { Job = job.ID });

        _cache[job.ID] = (requirements, result);
        return result;
    }
}
