// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.MalfAI;

/// <summary>
/// Tracks shunt state for a Malfunctioning AI brain entity (the entity with StationAiCoreComponent).
/// Stores the original core holder to allow returning to core.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MalfAiShuntedComponent : Component
{
    /// <summary>
    /// The entity that originally held the AI brain (the AI core entity that has the holding slot).
    /// Set on first successful shunt.
    /// </summary>
    [DataField]
    public EntityUid? CoreHolder;
    /// <summary>
    /// Whether the temporary APC StationAiHolderComponent was added by shunting.
    /// </summary>
    [NonSerialized]
    public bool AddedApcHolder;

    /// <summary>
    /// Whether the temporary APC StationAiCoreComponent was added by shunting.
    /// </summary>
    [NonSerialized]
    public bool AddedApcCore;
    /// <summary>
    /// The Return to Core action entity granted while shunted.
    /// Server-only runtime state; not networked.
    /// </summary>
    public EntityUid? ReturnAction;
}
