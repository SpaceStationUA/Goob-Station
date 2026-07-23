// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Common.Cyberdeck.Components;

/// <summary>
/// Relays an AI radial between a Cyberdeck user's body and their projected eye.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberdeckAiUiProxyComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? RemoteEntity;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? TargetEntity;
}
