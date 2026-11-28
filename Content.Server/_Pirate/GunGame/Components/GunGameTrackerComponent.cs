// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using Robust.Shared.Network;

namespace Content.Server._Pirate.GunGame.Components;

/// <summary>
/// This component is given to players at the start of the GunGame gamemode.
/// It is used to keep track of a player's currently spawned weapons.
/// <see cref="Content.Server._Pirate.GunGame.Components.GunGameRuleComponent.RewardSpawnsQueue"/>
/// </summary>

[RegisterComponent]
public sealed partial class GunGameTrackerComponent : Component
{
    public NetUserId? Player;
    public bool DeathRecorded;

    /// <summary>
    /// The list of current rewards given to the player.
    /// This is largely so the previous rewards can be deleted after the player gets a kill,
    /// since they should then be given the next weapon in their queue to replace it.
    /// </summary>
    [DataField]
    public List<EntityUid> CurrentRewards = new();
}
