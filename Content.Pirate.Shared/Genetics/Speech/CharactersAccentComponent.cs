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
/// Replaces individual characters with random strings, ignoring case etc.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CharactersAccentSystem))]
public sealed partial class CharactersAccentComponent : Component
{
    [DataField(required: true)]
    public Dictionary<char, List<string>> Chars = new();
}
