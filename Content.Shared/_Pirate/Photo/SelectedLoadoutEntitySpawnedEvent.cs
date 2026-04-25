// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

namespace Content.Shared._Pirate.Photo;

/// <summary>
/// Raised on entities spawned from a selected role loadout, not fixed job starting gear.
/// </summary>
public readonly record struct SelectedLoadoutEntitySpawnedEvent(EntityUid Owner);
