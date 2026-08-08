// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Speech.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Speech;

[RegisterComponent]
public sealed partial class SyllableObfuscationAccentComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SyllableObfuscationAccentPrototype> Accent;
}
