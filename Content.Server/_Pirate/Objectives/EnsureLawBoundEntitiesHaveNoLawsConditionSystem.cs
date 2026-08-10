// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Silicons.Laws;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Whitelist;

namespace Content.Server._Pirate.Objectives;

public sealed class EnsureLawBoundEntitiesHaveNoLawsConditionSystem : EntitySystem
{
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsureLawBoundEntitiesHaveNoLawsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<EnsureLawBoundEntitiesHaveNoLawsConditionComponent> ent,
        ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQueryEnumerator<SiliconLawBoundComponent>();
        var freeSilicons = 0;

        while (query.MoveNext(out var uid, out var lawBound))
        {
            if (!_whitelist.CheckBoth(uid, ent.Comp.LawEntityBlacklist, ent.Comp.LawEntityWhitelist))
                continue;

            if (_siliconLaw.GetLaws(uid, lawBound).Laws.Count == 0)
                freeSilicons++;
        }

        args.Progress = freeSilicons / (float) ent.Comp.EntitiesToFree;
    }
}
