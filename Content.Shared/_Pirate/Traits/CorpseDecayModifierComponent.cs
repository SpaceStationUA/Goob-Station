// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Traits;

[RegisterComponent]
public sealed partial class CorpseDecayModifierComponent : Component
{
    [DataField]
    public float Multiplier = 1f;
}
