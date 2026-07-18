// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Content.Shared.GameTicking;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Character.Info.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGameTicker), typeof(PirateSharedCharacterInfoSystem))]
public sealed partial class RoleplayInfoComponent : Component
{
    [DataField, AutoNetworkedField]
    public string OOCNotes = string.Empty;
}
