// SPDX-FileCopyrightText: 2025 Impstation contributors

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
    /// Imp. Which group of phrases the AAC tablet has access to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<QuickPhraseGroupPrototype> PhraseGroup;
}
