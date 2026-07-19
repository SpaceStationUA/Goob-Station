// SPDX-FileCopyrightText: 2025 Skye <57879983+Rainbeon@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Terkala <appleorange64@gmail.com>
// SPDX-FileCopyrightText: 2025 kbarkevich <24629810+kbarkevich@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Robust.Shared.GameStates;

namespace Content.Shared.BloodCult.Components;

/// <summary>
/// Angery fella.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JuggernautComponent : Component
{
	/// <summary>
	/// Whether the juggernaut is currently inactive (its source has been ejected).
	/// Inactive juggernauts cannot move or act, even if healed, until a soulstone or body is reinserted.
	/// </summary>
	[DataField, AutoNetworkedField]
	public bool IsInactive;
}
