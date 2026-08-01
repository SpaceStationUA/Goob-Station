// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Antags.SELF;

/// <summary>
/// Marks the SELF variant of a cryptographic sequencer.
/// </summary>
[RegisterComponent]
public sealed partial class FreeMagComponent : Component;

/// <summary>
/// Marks a silicon law provider that a FreeMAG may wipe directly.
/// </summary>
[RegisterComponent]
public sealed partial class FreeMagLawboardComponent : Component;
