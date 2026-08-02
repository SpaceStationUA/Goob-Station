// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared.Maps;

public sealed partial class GameMapPrototype
{
    [DataField]
    public string? MapIcon;

    [DataField]
    public EntProtoId? MapPreview;
}
