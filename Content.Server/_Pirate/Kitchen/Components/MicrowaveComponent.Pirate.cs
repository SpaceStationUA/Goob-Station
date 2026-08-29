// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Kitchen.Components;

public sealed partial class MicrowaveComponent
{
    /// <summary>
    /// The actor who last loaded or started this microwave, used to award cooking experience.
    /// </summary>
    [DataField]
    public EntityUid? LastUser;
}
