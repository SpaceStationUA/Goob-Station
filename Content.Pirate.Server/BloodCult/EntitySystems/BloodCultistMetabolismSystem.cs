// SPDX-FileCopyrightText: 2025 Terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.BloodCult;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.BloodCult.EntitySystems;

/// <summary>
/// Changes blood cultists' blood to Sanguine Perniculate
/// </summary>
public sealed class BloodCultistMetabolismSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<BloodCultistComponent, ComponentInit>(OnCultistInit);
        SubscribeLocalEvent<BloodCultistComponent, ComponentShutdown>(OnCultistShutdown);
        // Note: ComponentRemove is handled by BloodCultRuleSystem, so we use ComponentShutdown and EntityTerminatingEvent instead
        SubscribeLocalEvent<BloodCultistComponent, EntityTerminatingEvent>(OnCultistTerminating);
        

    }
    
    public override void Shutdown()
    {
        base.Shutdown();
    }

    private void OnCultistInit(EntityUid uid, BloodCultistComponent component, ComponentInit args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        var originalBlood = bloodstream.BloodReferenceSolution;
        component.OriginalBloodReagents = originalBlood.Clone();
        // Keep the reference volume intact so rejuvenation cannot reduce the bloodstream to 1u.
        var cultBlood = new Solution("SanguinePerniculate", originalBlood.Volume);

        try
        {
            _bloodstream.ChangeBloodReagents((uid, bloodstream), cultBlood);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to change blood type to SanguinePerniculate for {ToPrettyString(uid)}: {ex}");
        }
    }

    /// <summary>
    /// Handles cleanup when a cultist entity is being terminated.
    /// Also restores blood type if the component is still present (edge case where entity is deleted while still a cultist).
    /// </summary>
    private void OnCultistTerminating(EntityUid uid, BloodCultistComponent component, ref EntityTerminatingEvent args)
    {
        RestoreBlood(uid, component, " during termination");
    }

    private void OnCultistShutdown(EntityUid uid, BloodCultistComponent component, ComponentShutdown args)
    {
        RestoreBlood(uid, component);
    }

    private void RestoreBlood(EntityUid uid, BloodCultistComponent component, string context = "")
    {
        var originalBlood = component.OriginalBloodReagents;
        if (originalBlood == null ||
            !TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        try
        {
            _bloodstream.ChangeBloodReagents((uid, bloodstream), originalBlood);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to restore original blood for {ToPrettyString(uid)}{context}: {ex}");
        }
    }
}
