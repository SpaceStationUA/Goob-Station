using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Anomaly;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class ShadeskipPowerSystem : SharedShadeskipPowerSystem
{
    [Dependency] private readonly SharedAnomalySystem _anomalySystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    ///     Covers the surrounding area in shadow kudzu, briefly darkening it.
    /// </summary>
    protected override void OnPowerUsed(Entity<ShadeskipPowerComponent> psionic, ref ShadeskipPowerActionEvent args)
    {
        var performer = args.Performer;
        SpawnAtPosition("EffectFlashShadowkinShadeskip", Transform(performer).Coordinates);

        if (Transform(performer).GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("entity-anomaly-no-grid"), performer, performer);
            return;
        }

        var settings = new AnomalySpawnSettings
        {
            CanSpawnOnEntities = false,
            MinAmount = psionic.Comp.MinAmount,
            MaxAmount = psionic.Comp.MaxAmount,
            MaxRange = psionic.Comp.MaxRange,
        };

        var tiles = _anomalySystem.GetSpawningPoints(performer, 0.5f, 0.5f, settings, 1f);
        if (tiles is null)
            return;

        foreach (var tile in tiles)
            Spawn("ShadowKudzuWeak", _mapSystem.ToCenterCoordinates(tile, grid));

        AfterPowerUsed(psionic, args.Performer);
    }
}
