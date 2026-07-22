// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Pirate.Common.Cyberdeck.Components;

/// <summary>
/// Adds the extra delay and victim feedback used when hacking a silicon body.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberdeckSiliconTargetComponent : Component
{
    [DataField]
    public TimeSpan PenaltyTime = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier VictimHackedSound =
        new SoundPathSpecifier("/Audio/_Pirate/Effects/Cyberdeck/hack_victim.ogg", AudioParams.Default.WithVolume(6f));
}
