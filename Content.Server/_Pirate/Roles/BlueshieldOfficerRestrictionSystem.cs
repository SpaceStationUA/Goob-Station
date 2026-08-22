// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking.Events;
using Content.Server.Revolutionary.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Roles;

/// <summary>
/// Keeps the Blueshield Officer from joining before enough command staff are present,
/// including round-start assignments.
/// </summary>
public sealed class BlueshieldOfficerRestrictionSystem : EntitySystem
{
    // Pirate: three command members is practical for the server's usual population.
    private const int RequiredCommandStaff = 3;
    private static readonly ProtoId<JobPrototype> BlueshieldOfficerJob = "BlueshieldOfficer";

    public override void Initialize()
    {
        base.Initialize();

        // Late joins use GetDisallowedJobsEvent; explicit job requests use IsRoleAllowedEvent.
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (!HasEnoughCommandStaff())
            ev.Jobs.Add(BlueshieldOfficerJob);
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs?.Contains(BlueshieldOfficerJob) == true && !HasEnoughCommandStaff())
        {
            ev.Cancelled = true;
            ev.CancelReason = Loc.GetString("blueshield-officer-restriction");
        }
    }

    private bool HasEnoughCommandStaff()
    {
        var count = 0;
        var query = EntityQueryEnumerator<CommandStaffComponent>();

        while (query.MoveNext(out _, out var commandStaff))
        {
            if (!commandStaff.Enabled)
                continue;

            count++;
            if (count >= RequiredCommandStaff)
                return true;
        }

        return false;
    }
}
