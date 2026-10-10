// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.CustomMarkings;

/// <summary>Art as the database keeps it.</summary>
/// <param name="Png">Every frame on one PNG sheet, as <c>CustomMarkingArt.ToPng</c> lays them out.</param>
/// <param name="FrameTimes">Packed frame times, as <c>CustomMarkingRules.PackFrameTimes</c> makes; null for a still marking.</param>
/// <param name="Erase">The mask of erased body pixels; null for none.</param>
public sealed record CustomMarkingStoredArt(byte[] Png, byte[]? FrameTimes, byte[]? Erase);
