// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Content.Client.UserInterface.Systems.Character;
using Content.Shared._Pirate.Character.Info;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.Character.Info;

public sealed partial class PirateCharacterInfoSystem : PirateSharedCharacterInfoSystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    protected override void OpenCharacterWindow(EntityUid target, EntityUid requester)
    {
        _ui.GetUIController<CharacterUIController>().OpenInspectCharacterWindow(target, requester);
    }
}
