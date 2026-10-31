// Pirate: ported from ss14Starlight/space-station-14.
namespace Content.Server._Pirate.StarlightSpecies.Pollen.Components;

/// <summary>
/// Granted by Advanced Pollen's speed-buff outcome. While present, grants a
/// flat speed multiplier via RefreshMovementSpeedModifiersEvent. Removed (and
/// speed refreshed) once ExpiresAt passes - see PollenAdvancedSystem.Update.
/// Values are set at grant time from the triggering cloud's own tuning
/// (PollenAdvancedPollenComponent), not from defaults here.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenSpeedBuffComponent : Component
{
    public float WalkModifier;
    public float SprintModifier;

    [AutoPausedField]
    public TimeSpan ExpiresAt;
}
