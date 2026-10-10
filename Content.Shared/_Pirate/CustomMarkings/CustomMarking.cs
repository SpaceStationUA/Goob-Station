// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CustomMarkings;

/// <summary>Where a custom marking is drawn on a body.</summary>
[Serializable, NetSerializable]
public enum CustomMarkingPlacement : byte
{
    /// <summary>Behind the whole body: tails, wings and anything else seen past its edges.</summary>
    Behind,

    /// <summary>On the body, under underwear and clothing.</summary>
    Skin,

    /// <summary>Over the hands and feet, which draw above the uniform, and under gloves and shoes.</summary>
    Hands,

    /// <summary>Over the hair, under hats and masks.</summary>
    Hair,

    /// <summary>Over clothing.</summary>
    Front,
}

/// <summary>A custom marking a character wears: which art, and where it is drawn.</summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct CustomMarking
{
    /// <summary>Hash of the art's pixels, the id the server stores it under.</summary>
    public string Hash = string.Empty;

    public CustomMarkingPlacement Placement;

    public CustomMarking(string hash, CustomMarkingPlacement placement)
    {
        Hash = hash;
        Placement = placement;
    }
}

/// <summary>One saved marking in a player's library.</summary>
[Serializable, NetSerializable]
public readonly record struct CustomMarkingEntry(int Id, string Name, string Hash, CustomMarkingPlacement Placement);
