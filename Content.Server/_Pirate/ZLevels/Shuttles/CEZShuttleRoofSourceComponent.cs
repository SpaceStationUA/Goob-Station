using Robust.Shared.GameObjects;

namespace Content.Server._Pirate.ZLevels.Shuttles;

/// <summary>
/// Tags a shuttle's current top deck so <see cref="CEZShuttleRoofSystem"/> can resync the roof
/// when its tiles change (construction, deconstruction, combat damage). Runtime-only.
/// </summary>
[RegisterComponent]
public sealed partial class CEZShuttleRoofSourceComponent : Component
{
    public EntityUid Shuttle;
}
