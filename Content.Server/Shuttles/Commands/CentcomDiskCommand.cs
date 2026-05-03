using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Server.RoundEnd;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Shuttles.Commands;

/// <summary>
/// Spawns a CoordinatesDisk targeting CentCom and gives it to the caller (in a DiskCase if possible).
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class CentcomDiskCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntitySystemManager _entSystemManager = default!;

    public override string Command => "ccdisk";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        if (player.AttachedEntity == null)
        {
            shell.WriteLine(Loc.GetString("shell-must-be-attached-to-entity"));
            return;
        }

        var roundEnd = _entSystemManager.GetEntitySystem<RoundEndSystem>();
        var dest = roundEnd.GetCentcomm();
        if (dest is null)
        {
            shell.WriteError("No CentCom destination found.");
            return;
        }

        // Validate destination is a usable map
        if (!_entManager.TryGetComponent<MapComponent>(dest.Value, out var mapComp))
        {
            shell.WriteError(Loc.GetString("cmd-ftldisk-no-map-comp", ("destination", dest.Value), ("map", dest.Value)));
            return;
        }
        if (!mapComp.MapInitialized)
        {
            shell.WriteError(Loc.GetString("cmd-ftldisk-map-not-init", ("destination", dest.Value), ("map", dest.Value)));
            return;
        }
        if (mapComp.MapPaused)
        {
            shell.WriteError(Loc.GetString("cmd-ftldisk-map-paused", ("destination", dest.Value), ("map", dest.Value)));
            return;
        }

        // Ensure it's a valid FTL destination that requires a disk
        if (!_entManager.TryGetComponent<FTLDestinationComponent>(dest.Value, out var ftlDest))
        {
            ftlDest = _entManager.AddComponent<FTLDestinationComponent>(dest.Value);
            ftlDest.RequireCoordinateDisk = true;
        }

        var entity = player.AttachedEntity.Value;
        var coords = _entManager.GetComponent<TransformComponent>(entity).Coordinates;

        var handsSystem = _entSystemManager.GetEntitySystem<SharedHandsSystem>();
        var labelSystem = _entSystemManager.GetEntitySystem<LabelSystem>();
        var storageSystem = _entSystemManager.GetEntitySystem<SharedStorageSystem>();

        // Create disk and case
        EntityUid cdUid = _entManager.SpawnEntity(FTLDiskCommand.CoordinatesDisk, coords);
        var cd = _entManager.EnsureComponent<ShuttleDestinationCoordinatesComponent>(cdUid);
        cd.Destination = dest.Value;
        _entManager.Dirty(cdUid, cd);

        EntityUid cdCaseUid = _entManager.SpawnEntity(FTLDiskCommand.DiskCase, coords);

        // Label with destination name if available
        if (_entManager.TryGetComponent<MetaDataComponent>(dest.Value, out var meta) && meta?.EntityName != null)
        {
            labelSystem.Label(cdUid, meta.EntityName);
            labelSystem.Label(cdCaseUid, meta.EntityName);
        }

        // Try put disk into case, then put case in hand. Fallback: disk in hand.
        if (_entManager.TryGetComponent<StorageComponent>(cdCaseUid, out var storage) &&
            storageSystem.Insert(cdCaseUid, cdUid, out _, storageComp: storage, playSound: false))
        {
            if (_entManager.TryGetComponent<HandsComponent>(entity, out var handsComponent) &&
                handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
            {
                handsSystem.TryPickup(entity, cdCaseUid, emptyHand, checkActionBlocker: false, handsComp: handsComponent);
            }
        }
        else
        {
            _entManager.DeleteEntity(cdCaseUid);
            if (_entManager.TryGetComponent<HandsComponent>(entity, out var handsComponent) &&
                handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
            {
                handsSystem.TryPickup(entity, cdUid, emptyHand, checkActionBlocker: false, handsComp: handsComponent);
            }
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}