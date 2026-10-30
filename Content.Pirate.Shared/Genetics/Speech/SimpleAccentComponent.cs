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
/// Applies a single <see cref="SimpleAccentPrototype"/> to the speaking entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SimpleAccentComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SimpleAccentPrototype> Accent;
}
