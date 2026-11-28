// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.StationAi;

[RegisterComponent, Access(typeof(AiRadioJumpSystem))]
public sealed partial class AiRadioHistoryComponent : Component
{
    public readonly HashSet<EntityUid> Speakers = new();

    public TimeSpan NextJump;
}
