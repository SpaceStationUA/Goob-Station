// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.Antags.SELF;

[RegisterComponent, Access(typeof(SELFRecruitmentLetterSystem))]
public sealed partial class SELFRecruitmentLetterComponent : Component
{
    public bool Used;
}
