// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Paper;

// Pirate: direct texture paths required by Omu #2180.
public partial struct StampDisplayInfo
{
    [DataField]
    public string? StampLargePath;
}

public sealed partial class StampComponent
{
    [DataField]
    public string? StampLargePath;
}
