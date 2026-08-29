// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Pirate.Parry;

/// <summary>
/// Stores parry fatigue. Regeneration is calculated lazily when the value is next used,
/// so the system never scans all entities in Update.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class ParryExhaustionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Exhaustion;

    [DataField]
    public float MaxParryExhaustion = 0.25f;

    [DataField]
    public float MaxReflectExhaustion = 1f;

    [DataField]
    public float ExhaustionRegenRate = 0.1f;

    [DataField]
    public TimeSpan ExhaustionRegenDelay = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan ExhaustionRegenTimer;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan LastUpdate;
}
