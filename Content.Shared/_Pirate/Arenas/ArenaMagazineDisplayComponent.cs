// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Arenas;

/// <summary>Identifies a magazine-fed weapon issued to an arena participant.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArenaMagazineDisplayComponent : Component;
