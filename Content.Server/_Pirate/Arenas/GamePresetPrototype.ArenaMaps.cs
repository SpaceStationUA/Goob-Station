// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    /// <summary>Playable arena when the rotation excludes every map in the pool.</summary>
    [DataField]
    public ProtoId<GameMapPrototype>? ArenaFallbackMap { get; private set; }

    /// <summary>Optional arena an admin can select outside automatic rotation.</summary>
    [DataField]
    public ProtoId<GameMapPrototype>? ManualArenaMap { get; private set; }
}
