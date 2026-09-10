// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.CharacterPods;

[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class CharacterPodComponent : Component
{
    [DataField(required: true)]
    public ProtoId<RandomHumanoidSettingsPrototype> Settings;

    [DataField]
    public ComponentRegistry ExtraComponents = new();

    [DataField]
    public bool DeleteOnSpawn = true;

    [DataField]
    public int AvailableTakeovers = 1;

    #region Pirate: cybersun starting balance
    /// <summary>Initial bank balance for pods that bypass job spawning.</summary>
    [DataField]
    public int StartingBalance;
    #endregion Pirate: cybersun starting balance

    [ViewVariables]
    public int CurrentTakeovers;
}
