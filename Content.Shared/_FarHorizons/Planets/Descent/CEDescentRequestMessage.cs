using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Planets.Descent;

/// <summary>
/// Sent by the shuttle console BUI when the pilot confirms a descent onto a planet or an
/// ascent back to orbit. The server validates and runs the spinup theatre before handing
/// over to the descent sequence proper.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEDescentRequestMessage : BoundUserInterfaceMessage
{
    /// <summary>True = ascending from a planet's ground layer back to orbit.</summary>
    public bool Ascent;
}
