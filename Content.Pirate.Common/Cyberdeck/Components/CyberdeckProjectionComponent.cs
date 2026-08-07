// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Pirate.Common.Cyberdeck.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberdeckProjectionComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? RemoteEntity;

    [DataField]
    public SoundSpecifier CounterHackSound =
        new SoundPathSpecifier("/Audio/_Pirate/Effects/Cyberdeck/counterhack_ai.ogg", AudioParams.Default.WithVolume(6f));
}
