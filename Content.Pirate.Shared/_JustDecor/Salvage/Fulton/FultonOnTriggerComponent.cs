using System;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared._JustDecor.Salvage.Fulton;

/// <summary>
/// Automatically applies FultonedComponent to the entity when its MobState changes to a specific state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FultonOnTriggerComponent : Component
{
    /// <summary>
    /// The mob state that triggers the fulton.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("targetState"), AutoNetworkedField]
    public MobState TargetState = MobState.Critical;

    /// <summary>
    /// How long the fulton will remain before teleporting to the beacon.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("fultonDuration"), AutoNetworkedField]
    public TimeSpan FultonDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Linked fulton beacon.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("beacon"), AutoNetworkedField]
    public EntityUid? Beacon;

    /// <summary>
    /// Whether the fulton can be removed by others.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("removable"), AutoNetworkedField]
    public bool Removable = true;

    /// <summary>
    /// Sound that gets played when fulton is launched.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("sound"), AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Mining/fultext_launch.ogg");

    /// <summary>
    /// Whether the trigger is currently active.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("enabled"), AutoNetworkedField]
    public bool Enabled = true;
}
