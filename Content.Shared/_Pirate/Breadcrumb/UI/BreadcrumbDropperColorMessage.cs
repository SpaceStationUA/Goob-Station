// SPDX-FileCopyrightText: 2017-2026 Space Wizards Federation
// SPDX-FileCopyrightText: 2024-2026 Starlight
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Breadcrumb.UI;

/// <summary>
/// Sent when the player picks a preset color and hits submit in the UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class BreadcrumbDropperColorMessage(string color) : BoundUserInterfaceMessage
{
    public string Color = color;
}
