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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CharacterInspectInfoEvent>(OnInspectInfo);
    }

    protected override void OpenCharacterWindow(EntityUid target, EntityUid requester)
    {
        _ui.GetUIController<CharacterUIController>().OpenInspectCharacterWindow(target, requester);

        // The client's own mind is local; inspect requests fetch another mind's data from the server.
        if (target != requester)
            RaiseNetworkEvent(new RequestCharacterInspectInfoEvent(GetNetEntity(target)));
    }

    private void OnInspectInfo(CharacterInspectInfoEvent ev)
    {
        if (TryGetEntity(ev.Target, out var target))
            _ui.GetUIController<CharacterUIController>().ApplyInspectInfo(target.Value, ev);
    }
}
