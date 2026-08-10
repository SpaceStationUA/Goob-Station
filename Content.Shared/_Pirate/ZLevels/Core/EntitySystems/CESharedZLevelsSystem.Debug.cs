/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using SharedCCVars = Content.Shared.CCVar.CCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly INetManager _net = default!;

    private static readonly TimeSpan StairDebugRepeatWindow = TimeSpan.FromSeconds(0.5);

    private bool _zDebugEnabled;
    private bool _zDebugVerboseEnabled;
    private bool _zDebugStairsEnabled;
    private TimeSpan _nextStairDebugPrune;
    private readonly Dictionary<(EntityUid Uid, string EventName, string DedupeKey), TimeSpan> _stairDebugKeys = new();
    private readonly Dictionary<(EntityUid SourceGridUid, EntityUid PeerGridUid), TimeSpan> _watchedGridSyncPairs = new();

    private void InitializeDebug()
    {
        if (_net.IsServer)
        {
            _zDebugEnabled = _config.GetCVar(SharedCCVars.CEDebugMovement);
            _zDebugVerboseEnabled = _config.GetCVar(SharedCCVars.CEDebugMovementVerbose);
            _zDebugStairsEnabled = _config.GetCVar(SharedCCVars.CEDebugStairs);
            _config.OnValueChanged(SharedCCVars.CEDebugMovement, OnMovementDebugChanged);
            _config.OnValueChanged(SharedCCVars.CEDebugMovementVerbose, OnMovementVerboseDebugChanged);
            _config.OnValueChanged(SharedCCVars.CEDebugStairs, OnStairDebugChanged);
            return;
        }

        if (!_net.IsClient)
            return;

        _zDebugEnabled = _config.GetCVar(SharedCCVars.CEDebugMovementClient);
        _zDebugVerboseEnabled = _config.GetCVar(SharedCCVars.CEDebugMovementVerboseClient);
        _zDebugStairsEnabled = _config.GetCVar(SharedCCVars.CEDebugStairsClient);
        _config.OnValueChanged(SharedCCVars.CEDebugMovementClient, OnMovementDebugChanged);
        _config.OnValueChanged(SharedCCVars.CEDebugMovementVerboseClient, OnMovementVerboseDebugChanged);
        _config.OnValueChanged(SharedCCVars.CEDebugStairsClient, OnStairDebugChanged);
    }

    private void ShutdownDebug()
    {
        if (_net.IsServer)
        {
            _config.UnsubValueChanged(SharedCCVars.CEDebugMovement, OnMovementDebugChanged);
            _config.UnsubValueChanged(SharedCCVars.CEDebugMovementVerbose, OnMovementVerboseDebugChanged);
            _config.UnsubValueChanged(SharedCCVars.CEDebugStairs, OnStairDebugChanged);
            return;
        }

        if (!_net.IsClient)
            return;

        _config.UnsubValueChanged(SharedCCVars.CEDebugMovementClient, OnMovementDebugChanged);
        _config.UnsubValueChanged(SharedCCVars.CEDebugMovementVerboseClient, OnMovementVerboseDebugChanged);
        _config.UnsubValueChanged(SharedCCVars.CEDebugStairsClient, OnStairDebugChanged);
    }

    private void OnMovementDebugChanged(bool enabled)
    {
        if (_zDebugEnabled == enabled)
            return;

        _zDebugEnabled = enabled;
        Log.Info($"[CEZDebug] movement logging {(enabled ? "enabled" : "disabled")} via cvar {GetMovementDebugName()}");
    }

    private void OnMovementVerboseDebugChanged(bool enabled)
    {
        if (_zDebugVerboseEnabled == enabled)
            return;

        _zDebugVerboseEnabled = enabled;
        Log.Info($"[CEZDebug] verbose movement logging {(enabled ? "enabled" : "disabled")} via cvar {GetMovementVerboseDebugName()}");
    }

    private void OnStairDebugChanged(bool enabled)
    {
        if (_zDebugStairsEnabled == enabled)
            return;

        _zDebugStairsEnabled = enabled;
        _stairDebugKeys.Clear();
        _watchedGridSyncPairs.Clear();
        _nextStairDebugPrune = TimeSpan.Zero;
        Log.Info($"[CEZStairCsv] stair logging {(enabled ? "enabled" : "disabled")} via cvar {GetStairDebugName()}");
    }

    protected bool ZDebugEnabled => _zDebugEnabled;
    protected bool ZDebugVerboseEnabled => ZDebugEnabled && _zDebugVerboseEnabled;
    protected bool ZDebugStairsEnabled => _zDebugStairsEnabled;

    private string GetMovementDebugName()
    {
        return _net.IsServer
            ? SharedCCVars.CEDebugMovement.Name
            : SharedCCVars.CEDebugMovementClient.Name;
    }

    private string GetMovementVerboseDebugName()
    {
        return _net.IsServer
            ? SharedCCVars.CEDebugMovementVerbose.Name
            : SharedCCVars.CEDebugMovementVerboseClient.Name;
    }

    private string GetStairDebugName()
    {
        return _net.IsServer
            ? SharedCCVars.CEDebugStairs.Name
            : SharedCCVars.CEDebugStairsClient.Name;
    }

    protected string StairCsvSide()
    {
        if (_net.IsServer)
            return "server";

        if (_net.IsClient)
            return "client";

        return "unknown";
    }

    protected string StairCsvRemainingTime(TimeSpan targetTime)
    {
        var remaining = targetTime - _timing.CurTime;
        return StairCsvFloat((float) Math.Max(0, remaining.TotalSeconds));
    }

    protected static bool ShouldLogMovementTick(CEZPhysicsComponent zPhys, float oldHeight)
    {
        return oldHeight > 0.01f
               || zPhys.LocalPosition > 0.01f
               || zPhys.CurrentGroundHeight > 0.01f
               || zPhys.CurrentGroundHeight < -0.01f
               || zPhys.CurrentStickyGround;
    }

    protected void DebugZVerbose(EntityUid ent, string message)
    {
        if (!ZDebugVerboseEnabled)
            return;

        DebugZ(ent, message);
    }

    protected void DebugZ(EntityUid ent, string message)
    {
        if (!ZDebugEnabled)
            return;

        var xform = Transform(ent);
        if (!HasTraversalContext(xform))
            return;

        var zState = ZPhysQuery.TryComp(ent, out var zPhys)
            ? $" local={zPhys.LocalPosition:0.00} vel={zPhys.Velocity:0.00} ground={zPhys.CurrentGroundHeight:0.00} sticky={zPhys.CurrentStickyGround}"
            : string.Empty;

        Log.Info($"[CEZDebug] {ToPrettyString(ent)} parent={xform.ParentUid} grid={xform.GridUid} map={xform.MapUid}{zState} :: {message}");
    }

    protected static string StairCsvFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    protected static string StairCsvBool(bool value)
    {
        return value ? "1" : "0";
    }

    protected static string StairCsvVec2(Vector2 value)
    {
        return $"{StairCsvFloat(value.X)}:{StairCsvFloat(value.Y)}";
    }

    protected static string StairCsvDedupeFloat(float value, int decimals)
    {
        return StairCsvFloat(MathF.Round(value, decimals));
    }

    protected static string StairCsvDedupeVec2(Vector2 value, int decimals)
    {
        return $"{StairCsvDedupeFloat(value.X, decimals)}:{StairCsvDedupeFloat(value.Y, decimals)}";
    }

    protected void WatchGridSyncPair(EntityUid sourceGridUid, EntityUid peerGridUid)
    {
        if (!ZDebugStairsEnabled ||
            sourceGridUid == EntityUid.Invalid ||
            peerGridUid == EntityUid.Invalid)
        {
            return;
        }

        PruneWatchedGridSyncPairs();
        var expiresAt = _timing.CurTime + StairDebugRepeatWindow;
        _watchedGridSyncPairs[(sourceGridUid, peerGridUid)] = expiresAt;
        _watchedGridSyncPairs[(peerGridUid, sourceGridUid)] = expiresAt;
    }

    protected bool IsGridSyncPairWatched(EntityUid sourceGridUid, EntityUid peerGridUid)
    {
        if (!ZDebugStairsEnabled ||
            sourceGridUid == EntityUid.Invalid ||
            peerGridUid == EntityUid.Invalid)
        {
            return false;
        }

        if (!_watchedGridSyncPairs.TryGetValue((sourceGridUid, peerGridUid), out var expiresAt))
            return false;

        if (_timing.CurTime <= expiresAt)
            return true;

        _watchedGridSyncPairs.Remove((sourceGridUid, peerGridUid));
        _watchedGridSyncPairs.Remove((peerGridUid, sourceGridUid));
        return false;
    }

    private void PruneWatchedGridSyncPairs()
    {
        if (_watchedGridSyncPairs.Count == 0)
            return;

        var expired = new List<(EntityUid SourceGridUid, EntityUid PeerGridUid)>();
        foreach (var (pair, expiresAt) in _watchedGridSyncPairs)
        {
            if (_timing.CurTime > expiresAt)
                expired.Add(pair);
        }

        foreach (var pair in expired)
        {
            _watchedGridSyncPairs.Remove(pair);
        }
    }

    protected bool DebugZStairCsv(EntityUid ent, string eventName, string payload, string? dedupeKey = null)
    {
        if (!ZDebugStairsEnabled)
            return false;

        (EntityUid Uid, string EventName, string DedupeKey)? pendingDedupeKey = null;
        if (dedupeKey != null)
        {
            PruneStairDebugKeys();

            var key = (ent, eventName, dedupeKey);
            if (_stairDebugKeys.TryGetValue(key, out var previousTime) &&
                _timing.CurTime - previousTime < StairDebugRepeatWindow)
                return false;

            pendingDedupeKey = key;
        }

        var xform = Transform(ent);
        if (!HasTraversalContext(xform))
            return false;

        if (pendingDedupeKey is { } persistKey)
            _stairDebugKeys[persistKey] = _timing.CurTime;

        var basePayload =
            $"uid={ToPrettyString(ent)},parent={xform.ParentUid},grid={xform.GridUid},map={xform.MapUid}";

        if (ZPhysQuery.TryComp(ent, out var zPhys))
        {
            basePayload +=
                $",local={StairCsvFloat(zPhys.LocalPosition)},vel={StairCsvFloat(zPhys.Velocity)},ground={StairCsvFloat(zPhys.CurrentGroundHeight)},sticky={StairCsvBool(zPhys.CurrentStickyGround)},current_z={zPhys.CurrentZLevel},from_below={StairCsvBool(zPhys.CurrentGroundFromBelowLevel)},support_below={StairCsvBool(zPhys.CurrentHasSupportBelow)},highground_below={StairCsvBool(zPhys.CurrentHighGroundBelow)},up_block_rem={StairCsvRemainingTime(zPhys.AutoUpBlockedUntil)},down_block_rem={StairCsvRemainingTime(zPhys.AutoDownBlockedUntil)},startup_block_rem={StairCsvRemainingTime(zPhys.StartupSuppressedUntil)}";
        }

        basePayload += $",side={StairCsvSide()},first_pred={StairCsvBool(_timing.IsFirstTimePredicted)},applying_state={StairCsvBool(_timing.ApplyingState)}";

        Log.Info($"[CEZStairCsv] event={eventName},{basePayload},{payload}");
        return true;
    }

    private void PruneStairDebugKeys()
    {
        if (_stairDebugKeys.Count == 0 ||
            _timing.CurTime < _nextStairDebugPrune)
            return;

        _nextStairDebugPrune = _timing.CurTime + StairDebugRepeatWindow;
        var expired = new List<(EntityUid Uid, string EventName, string DedupeKey)>();

        foreach (var (key, previousTime) in _stairDebugKeys)
        {
            if (_timing.CurTime - previousTime >= StairDebugRepeatWindow)
                expired.Add(key);
        }

        foreach (var key in expired)
        {
            _stairDebugKeys.Remove(key);
        }
    }
}
