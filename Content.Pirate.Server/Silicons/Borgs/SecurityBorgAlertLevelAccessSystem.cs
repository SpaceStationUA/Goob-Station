// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Silicons.Borgs;

/// <summary>
/// Applies the security cyborg alert policy to its mutable access provider.
/// </summary>
public sealed class SecurityBorgAlertLevelAccessSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> SecurityOfficerJob = "SecurityOfficer";
    private static readonly ProtoId<AccessGroupPrototype> AllAccessGroup = "AllAccess";
    private static readonly ProtoId<AccessLevelPrototype> BorgAccess = "Borg";
    private static readonly ProtoId<AccessLevelPrototype> MaintenanceAccess = "Maintenance";

    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityBorgAlertLevelAccessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SecurityBorgAlertLevelAccessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SecurityBorgAlertLevelAccessComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnStartup(Entity<SecurityBorgAlertLevelAccessComponent> ent, ref ComponentStartup args)
    {
        UpdateAccess(ent.Owner);
    }

    private void OnShutdown(Entity<SecurityBorgAlertLevelAccessComponent> ent, ref ComponentShutdown args)
    {
        ApplyFullAccess(ent.Owner);
    }

    private void OnParentChanged(Entity<SecurityBorgAlertLevelAccessComponent> ent, ref EntParentChangedMessage args)
    {
        UpdateAccess(ent.Owner);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<SecurityBorgAlertLevelAccessComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_station.GetOwningStation(uid) == args.Station)
                ApplyAccess(uid, SecurityBorgAlertLevelPolicy.GetTier(args.AlertLevel));
        }
    }

    private void UpdateAccess(EntityUid uid)
    {
        var alertLevel = _station.GetOwningStation(uid) is { } station
            ? _alertLevel.GetLevel(station)
            : null;

        ApplyAccess(uid, SecurityBorgAlertLevelPolicy.GetTier(alertLevel));
    }

    private void ApplyAccess(EntityUid uid, SecurityBorgAlertLevelTier tier)
    {
        if (!HasComp<AccessComponent>(uid))
            return;

        if (tier == SecurityBorgAlertLevelTier.Full)
        {
            ApplyFullAccess(uid);
            return;
        }

        var officer = _prototypes.Index(SecurityOfficerJob);
        _access.SetAccessToJob(uid, officer, false);

        var tags = _access.TryGetTags(uid)?.ToHashSet() ?? [];
        tags.Add(BorgAccess);
        if (tier == SecurityBorgAlertLevelTier.Green)
            tags.Remove(MaintenanceAccess);

        _access.TrySetTags(uid, tags);
    }

    private void ApplyFullAccess(EntityUid uid)
    {
        if (!HasComp<AccessComponent>(uid))
            return;

        var tags = _prototypes.Index(AllAccessGroup).Tags.ToHashSet();
        tags.Add(BorgAccess);
        _access.TrySetTags(uid, tags);
    }
}
