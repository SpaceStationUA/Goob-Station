// SPDX-FileCopyrightText: 2026 kotobdev <59124164+kotobdev@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.Avali.Components;

/// <summary>
/// Enables Avali feather preening, shedding, and regrowth.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PreenableComponent : Component
{
    [DataField]
    public EntProtoId FeatherPrototype;

    [DataField]
    public HashSet<ProtoId<DamageGroupPrototype>>? ValidDamageGroups = new()
    {
        "Brute",
    };

    [DataField]
    public LocId SelfPreeningMessage = "preening-popup-self";

    [DataField]
    public LocId GettingPreenedMessage = "preening-popup-self-recipient";

    [DataField]
    public LocId PreeningOtherMessage = "preening-popup-other";

    [DataField]
    public LocId FeatherBloodiedNameString = "feather-bloody-name-modifier";

    [DataField]
    public LocId FeatherBloodiedDescString = "feather-bloody-desc";

    [DataField]
    public LocId PreeningVerbString = "preening-action-verb";

    [DataField]
    public LocId DroppedFeatherString = "preening-feather-dropped-injured";

    [DataField]
    public ProtoId<EmotePrototype> ScreamEmote = "Scream";

    /// <summary>
    /// The minimum applicable damage an attack must exceed before it can shed a feather.
    /// </summary>
    [DataField]
    public FixedPoint2 ShedDamageThreshold = 9;

    /// <summary>
    /// Chance to shed a feather per point of applicable damage taken.
    /// </summary>
    [DataField]
    public float ShedScalingChance = 0.0125f;

    [DataField]
    public DamageModifierSet? VulnerabilityModifier;

    [DataField, AutoNetworkedField]
    public int MaximumFeathers = 3;

    [DataField, AutoNetworkedField]
    public int CurrentFeathers;

    [DataField]
    public TimeSpan ReplenishDelay = TimeSpan.FromSeconds(150);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? ReplenishTime;
}
