// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.ZLevels.View;

/// <summary>
/// Server-side bookkeeping for UIs whose engine range check is lifted while a station AI uses them from
/// another deck. <see cref="ZStationAiDeckUiSystem"/> enforces the range for every actor instead.
/// </summary>
[RegisterComponent, Access(typeof(ZStationAiDeckUiSystem))]
public sealed partial class ZStationAiDeckUiComponent : Component
{
    /// <summary>
    /// UI key to the interaction range the engine enforced before it was lifted.
    /// </summary>
    [ViewVariables]
    public Dictionary<Enum, float> LiftedRanges = new();
}
