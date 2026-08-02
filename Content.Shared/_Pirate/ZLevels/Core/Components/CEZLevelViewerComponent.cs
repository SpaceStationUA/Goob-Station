/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Allows entity to see through Z-levels
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent, Access(typeof(CESharedZLevelsSystem))]
public sealed partial class CEZLevelViewerComponent : Component
{
    /// <summary>Fixed slot count for stair preview positions. Networked individually as separate fields.</summary>
    public const int MaxStairPreviewPositions = 4;

    public HashSet<EntityUid> Eyes = new();

    /// <summary>
    /// We can look at 1 z-level up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LookUp;

    /// <summary>
    /// True when one or more nearby high-ground (stair) tiles allow a preview of the level above.
    /// Mutually exclusive with <see cref="LookUp"/> for routing the +1 probe.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StairPreviewUp;

    /// <summary>
    /// Number of stair preview origins currently active (0..<see cref="MaxStairPreviewPositions"/>).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StairPreviewPositionCount;

    /// <summary>
    /// Primary world position on the viewer's current map to use as the FOV/PVS origin for stair preview.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition2;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition3;

    [DataField, AutoNetworkedField]
    public Vector2 StairPreviewPosition4;

    [DataField]
    public EntProtoId ActionProto = "CEActionToggleLookUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelActionEntity;

    public Vector2 GetStairPreviewPosition(int index)
    {
        return index switch
        {
            0 => StairPreviewPosition,
            1 => StairPreviewPosition2,
            2 => StairPreviewPosition3,
            3 => StairPreviewPosition4,
            // Surface a drift from MaxStairPreviewPositions instead of silently returning (0,0).
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, $"Stair preview index must be 0..{MaxStairPreviewPositions - 1}."),
        };
    }

    public void SetStairPreviewPosition(int index, Vector2 value)
    {
        switch (index)
        {
            case 0: StairPreviewPosition = value; break;
            case 1: StairPreviewPosition2 = value; break;
            case 2: StairPreviewPosition3 = value; break;
            case 3: StairPreviewPosition4 = value; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Stair preview index must be 0..{MaxStairPreviewPositions - 1}.");
        }
    }
}
