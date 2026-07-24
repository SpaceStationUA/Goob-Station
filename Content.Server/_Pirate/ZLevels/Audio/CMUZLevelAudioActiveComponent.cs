// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.GameStates;

namespace Content.Server._Pirate.ZLevels.Audio;

/// <summary>
/// Runtime-only marker for audio sources currently eligible for cross-Z projection.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class CMUZLevelAudioActiveComponent : Component;
