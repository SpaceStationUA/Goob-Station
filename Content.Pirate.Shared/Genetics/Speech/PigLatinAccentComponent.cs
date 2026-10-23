// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Speech.Components;

namespace Content.Pirate.Shared.Genetics.Speech;

/// <summary>
/// Accent that makes you speak in pig latin.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PigLatinAccentComponent : Component;
