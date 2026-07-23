// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Common.Cyberdeck.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CyberdeckSourceComponent : Component
{
    /// <summary>
    /// Server-side cache used to refresh the alert only when derived charges change.
    /// </summary>
    [ViewVariables]
    public int? LastObservedCharges;
}
