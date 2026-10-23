// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Common.Genetics.Speech;

/// <summary>
/// Event raised on a speech source to allow replacing the font used.
/// This overrides languages.
/// </summary>
[ByRefEvent]
public record struct SpeechFontOverrideEvent(EntityUid Source, string Font);
