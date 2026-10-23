// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Trigger.Components.Effects;

namespace Content.Pirate.Shared.Genetics.Trigger;

/// <summary>
/// Mutation component to perform a <c>ActionMutationComponent</c> instant action when this mutation entity gets triggered.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(MutationActionOnTriggerSystem))]
[AutoGenerateComponentState]
public sealed partial class MutationActionOnTriggerComponent : BaseXOnTriggerComponent;
