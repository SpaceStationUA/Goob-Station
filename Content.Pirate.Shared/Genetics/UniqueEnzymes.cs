// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Humanoid;
using Robust.Shared.Enums;

namespace Content.Pirate.Shared.Genetics;

/// <summary>
/// Basic appearance features that can be applied to a Mutatable mob.
/// Hair isn't included since you can do that with barber scissors / mirror.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct UniqueEnzymes(string Name, string? Prints, Sex? Sex, Gender? Gender, Color? EyeColor, Color? SkinColor);
