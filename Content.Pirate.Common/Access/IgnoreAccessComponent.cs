// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Common.Access;

/// <summary>
/// Allows selected entities to bypass an access reader until the component is removed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IgnoreAccessComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public HashSet<EntityUid> Ignore = new();
}
