/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float>
        CEBaseFallingDamage = CVarDef.Create("zlevels.ce_base_falling_damage", 1.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEBaseFallingOtherDamage = CVarDef.Create("zlevels.ce_base_falling_other_damage", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEBaseFallingStunTime = CVarDef.Create("zlevels.ce_base_falling_stun_time", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        CEBaseFallingOtherStunTime = CVarDef.Create("zlevels.ce_base_falling_other_stun_time", 0.01f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool>
        CEPostProcess = CVarDef.Create("shaders.ce_post_process", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int>
        CEZLevelsVisibleBelow = CVarDef.Create("zlevels.ce_visible_below", 6, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Parallax offset (in tiles) applied per depth step when rendering Z-levels above/below.
    /// Each rendered deck is shifted by this * its depth, faking perspective. Also drives the
    /// per-entity vertical eye offset for entities at a non-zero local Z position.
    /// Set live from the console (<c>zlevels.ce_render_offset</c>) to preview values; 0 stacks
    /// decks with no offset. Default 0.7.
    /// </summary>
    public static readonly CVarDef<float>
        CEZLevelsRenderOffset = CVarDef.Create("zlevels.ce_render_offset", 0.3f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEDebugMovement = CVarDef.Create("zlevels.ce_debug_movement", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEDebugMovementVerbose = CVarDef.Create("zlevels.ce_debug_movement_verbose", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEDebugStairs = CVarDef.Create("zlevels.ce_debug_stairs", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEDebugMovementClient = CVarDef.Create("zlevels.ce_debug_movement_client", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEDebugMovementVerboseClient = CVarDef.Create("zlevels.ce_debug_movement_verbose_client", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEDebugStairsClient = CVarDef.Create("zlevels.ce_debug_stairs_client", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Internal z-physics tick rate, in Hz. The Update loop accumulates engine frametime and
    /// advances physics in fixed-size substeps of <c>1/this</c> seconds. Defaults to 30 Hz to
    /// match the typical engine tickrate (one substep per engine tick, no behavior change).
    /// Bump it for smoother z-physics on hosts that run faster; capped at
    /// <see cref="CESharedZLevelsSystem.MaxStepsPerFrame"/> substeps per frame either way.
    /// </summary>
    public static readonly CVarDef<float>
        CEZPhysicsTickRate = CVarDef.Create("zlevels.ce_physics_tickrate", 30f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Maximum number of Z-level transitions (a fall or auto-climb across maps) the server
    /// will process per simulation tick. Caps the worst-case cost of a tower-collapse event.
    /// Set to 0 to disable Z-transitions entirely. Default 64.
    /// </summary>
    public static readonly CVarDef<int>
        CEZMaxTransitionsPerTick = CVarDef.Create("zlevels.ce_max_transitions_per_tick", 64, CVar.SERVERONLY);

    /// <summary>
    /// Wallclock budget (milliseconds) per tick for processing Z-level transitions. Once
    /// exceeded, further transitions defer to the next tick. Compounds with
    /// <see cref="CEZMaxTransitionsPerTick"/> — whichever limit is hit first wins. 0 disables
    /// the wallclock check (count-only). Default 1 ms.
    /// </summary>
    public static readonly CVarDef<float>
        CEZTransitionBudgetMs = CVarDef.Create("zlevels.ce_transition_budget_ms", 1f, CVar.SERVERONLY);

    /// <summary>
    /// If true, audio playing on a Z-network map is also projected to adjacent Z layers through
    /// floor/ceiling openings. Adds ~one PlayStatic call per audible adjacent layer per
    /// audio entity. Toggle off to disable cross-Z hearing.
    /// </summary>
    public static readonly CVarDef<bool>
        CEZLevelsCrossZAudio = CVarDef.Create("zlevels.ce_cross_z_audio", true, CVar.SERVERONLY);

    /// <summary>
    /// Debug-only: log every decision gate in the cross-Z audio projection pipeline. Use to
    /// figure out why a given sound isn't reaching a listener on an adjacent level.
    /// </summary>
    public static readonly CVarDef<bool>
        CEZLevelsCrossZAudioDebug = CVarDef.Create("zlevels.ce_cross_z_audio_debug", false, CVar.SERVERONLY);

    /// <summary>
    /// Maximum number of probe-eyes (adjacent Z layers visible to a player) the server will
    /// maintain per viewer. Caps PVS expansion on deep z-networks. Default 5.
    /// </summary>
    public static readonly CVarDef<int>
        CEZMaxViewProbesPerPlayer = CVarDef.Create("zlevels.ce_max_view_probes_per_player", 5, CVar.SERVERONLY);

    /// <summary>
    /// Floor for a probe-eye's PvsScale. Allows shrinking adjacent-layer PVS relative to the
    /// viewer's own. The viewer's PvsScale still wins if larger. Default 1.0 (no shrink).
    /// </summary>
    public static readonly CVarDef<float>
        CEZMinProbePvsScale = CVarDef.Create("zlevels.ce_min_probe_pvs_scale", 1f, CVar.SERVERONLY);

    /// <summary>
    /// How often (Hz) the server re-evaluates which Z layers a viewer needs probes on.
    /// Lower = calmer, higher = more responsive but more churn. Default 4 Hz.
    /// </summary>
    public static readonly CVarDef<float>
        CEZProbeUpdateHz = CVarDef.Create("zlevels.ce_probe_update_hz", 4f, CVar.SERVERONLY);

    // --- Projected lighting (client-only fake light through Z openings) ----------------------

    /// <summary>If true, lights on adjacent Z layers spawn client-only projected lights at floor openings on the viewer's map.</summary>
    public static readonly CVarDef<bool>
        CEZProjectedLightingEnabled = CVarDef.Create("zlevels.ce_projected_lighting_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Maximum number of projected lights per adjacent Z layer. Caps render cost. Default 16.</summary>
    public static readonly CVarDef<int>
        CEZMaxProjectedLightsPerLevel = CVarDef.Create("zlevels.ce_max_projected_lights_per_level", 16, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How much each depth step attenuates projected light energy. Higher = darker further away.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightAttenuationPerDepth = CVarDef.Create("zlevels.ce_projected_light_attenuation_per_depth", 0.75f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How much each tile of distance from source to opening attenuates projected light energy.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightAttenuationPerTile = CVarDef.Create("zlevels.ce_projected_light_attenuation_per_tile", 0.25f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Maximum radius for any single projected light. Caps individual brightness footprint.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightMaxRadius = CVarDef.Create("zlevels.ce_projected_light_max_radius", 4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Multiplier on the source-light remaining-radius to compute projected radius.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightRadiusScale = CVarDef.Create("zlevels.ce_projected_light_radius_scale", 0.6f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Energy floor below which a projected light is discarded as imperceptible.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightMinEnergy = CVarDef.Create("zlevels.ce_projected_light_min_energy", 0.1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Max world distance a cross-Z shot may travel. Cross-Z is intentionally close-quarters. Default 4.</summary>
    public static readonly CVarDef<float>
        CEZShootingRange = CVarDef.Create("zlevels.ce_shooting_range", 4f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Max tile distance from the shooter to an eligible floor-opening center. Default 2.</summary>
    public static readonly CVarDef<float>
        CEZShootingOpeningTileRange = CVarDef.Create("zlevels.ce_shooting_opening_tile_range", 2f, CVar.SERVER | CVar.REPLICATED);
}
