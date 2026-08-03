// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.QuickPhrase;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.AACTablet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AACTabletComponent : Component
{
    // Minimum time between each phrase, to prevent spam
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    // Time that the next phrase can be sent.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPhrase;

    /// <summary>
    /// Limits this tablet to a custom phrase set. A null value exposes the full catalog.
    /// </summary>
    [DataField]
    public ProtoId<QuickPhraseGroupPrototype>? PhraseGroup;
}
