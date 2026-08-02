// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Audio;

/// <summary>
/// Marks station radio audio projected away from its receiver by multiz audio.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StationRadioReceiverAudioComponent : Component;
