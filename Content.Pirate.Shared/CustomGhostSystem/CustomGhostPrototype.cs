using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.CustomGhostSystem;

[DataDefinition]
[Prototype("customGhost")]
public sealed partial class CustomGhostPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("ckey", required: true)]
    public string Ckey { get; set; } = default!;

    [DataField("sprite", required: true)]
    public List<ResPath> CustomSpritePath { get; set; } = new();

    [DataField("alpha")]
    public float AlphaOverride { get; set; } = -1;

    /// <summary>Multiplier for the server-wide pixel limit; 0 disables it.</summary>
    [DataField("maxSize")]
    public float MaxSize { get; set; } = 1f;

    [DataField("ghostName")]
    public string GhostName = string.Empty;

    [DataField("ghostDescription")]
    public string GhostDescription = string.Empty;
}

[Serializable, NetSerializable]
public enum CustomGhostAppearance
{
    Sprite,
    AlphaOverride,
    /// <summary>Prototype size multiplier applied by the client.</summary>
    MaxSize
}
