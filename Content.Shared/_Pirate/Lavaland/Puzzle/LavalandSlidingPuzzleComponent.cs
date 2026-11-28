// SPDX-FileCopyrightText: 2026 CrimeMoot and Inconnu1337
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Lavaland.Puzzle;

[RegisterComponent]
public sealed partial class LavalandSlidingPuzzleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Elements = new();

    [ViewVariables, DataField]
    public int EmptyTileId = 0;

    [DataField]
    public bool RequiresPrisoner;

    [ViewVariables]
    public bool Finished = false;

    [DataField]
    public string ElementProtoPrefix = "LavalandPuzzlePillar";

    [DataField]
    public EntProtoId? RewardProto;

    [DataField]
    public string PieceProtoPrefix = "LavalandPuzzlePieceFloor";

    /// <summary>
    /// Chance of an additional megafauna encounter if none of this species is alive on the map.
    /// </summary>
    [DataField]
    public float MegafaunaChance = 0.25f;

    [DataField]
    public EntProtoId? MegafaunaProto;

    [DataField]
    public float RecoilRadius = 7f;

    [DataField]
    public float RecoilStrength = 2f;

    [DataField]
    public SoundSpecifier? SolveSound;

    [ViewVariables]
    public EntityUid? Prisoner;

    public bool AddedMovementBlock;
    public bool AddedGodmode;

    [DataField]
    public EntProtoId? ReturnCubeProto;

    [ViewVariables]
    public EntityUid? GeneratedGrid;

    [DataField]
    public ProtoId<SourcePortPrototype>? SolvedPort;

    [DataField]
    public LocId SolvedMessage = "sliding-puzzle-solved";
}

[RegisterComponent]
public sealed partial class LavalandSlidingPuzzleElementComponent : Component
{
    [ViewVariables]
    public int Id = 0;

    [ViewVariables]
    public EntityUid Source;
}

[RegisterComponent]
public sealed partial class LavalandPrisonCubeComponent : Component
{
    [DataField]
    public EntProtoId PuzzleProto = "LavalandPrisonCubePuzzle";

    [DataField]
    public SoundSpecifier ActivateSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
}
