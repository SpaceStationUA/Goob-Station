// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TastyBloodComponent : Component
{
    /// <summary>
    /// Locale key of this species' blood taste. Used as a prefix when describing blood.
    /// </summary>
    [DataField]
    public LocId RaceTaste = "bloodsucker-taste-race-generic";

    /// <summary>
    /// Player-defined taste description from the character profile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string CustomTaste = string.Empty;

    /// <summary>
    /// Whether this blood actually grants the bloodsucker its usual benefits.
    /// The component only marks the blood as tasty; species such as synth, plasmaman and
    /// slimeperson still have a taste, but their blood nourishes nobody.
    /// </summary>
    [DataField]
    public bool GivesBenefits = true;
}
