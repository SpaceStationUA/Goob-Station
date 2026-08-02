// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Server.AACTablet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AACTabletComponent : Component
{
    // Minimum time between each phrase, to prevent spam
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    // Time that the next phrase can be sent.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPhrase;
}
