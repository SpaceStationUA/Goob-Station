// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Paper;

/// <summary>
/// Prevents this entity from opening the paper UI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlockReadingComponent : Component;
