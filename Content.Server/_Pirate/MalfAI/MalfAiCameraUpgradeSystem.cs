// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.MalfAI;
using Content.Shared.Silicons.StationAi;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
/// Handles the Malf AI camera-upgrade toggle and keeps the Effective flag in sync with core status.
/// </summary>
public sealed class MalfAiCameraUpgradeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Component lifecycle events allow one subscriber per component/event pair.
        // Keep both camera upgrades here, including AIs that only bought microphones.
        SubscribeLocalEvent<StationAiHeldComponent, ComponentStartup>(OnHeldStartup);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentShutdown>(OnHeldShutdown);
        SubscribeLocalEvent<MalfAiMarkerComponent, MalfAiCameraUpgradeUnlockedEvent>(OnCameraUpgradeUnlocked);
    }

    private void OnCameraUpgradeUnlocked(EntityUid uid, MalfAiMarkerComponent marker, MalfAiCameraUpgradeUnlockedEvent ev)
    {
        var comp = EnsureComp<MalfAiCameraUpgradeComponent>(uid);
        comp.EnabledDesired = true;
        comp.EnabledEffective = HasComp<StationAiHeldComponent>(uid);
        Dirty(uid, comp);
    }

    private void OnHeldStartup(EntityUid uid, StationAiHeldComponent held, ref ComponentStartup args)
    {
        if (TryComp<MalfAiCameraMicrophonesComponent>(uid, out var microphones))
        {
            microphones.EnabledEffective = microphones.EnabledDesired;
            Dirty(uid, microphones);
        }

        if (!TryComp(uid, out MalfAiCameraUpgradeComponent? comp))
            return;

        var newEffective = comp.EnabledDesired;
        if (comp.EnabledEffective != newEffective)
        {
            comp.EnabledEffective = newEffective;
            Dirty(uid, comp);
        }
    }

    private void OnHeldShutdown(EntityUid uid, StationAiHeldComponent held, ref ComponentShutdown args)
    {
        if (TryComp<MalfAiCameraMicrophonesComponent>(uid, out var microphones) && microphones.EnabledEffective)
        {
            microphones.EnabledEffective = false;
            Dirty(uid, microphones);
        }

        if (!TryComp(uid, out MalfAiCameraUpgradeComponent? comp))
            return;

        if (comp.EnabledEffective)
        {
            comp.EnabledEffective = false;
            Dirty(uid, comp);
        }
    }
}
