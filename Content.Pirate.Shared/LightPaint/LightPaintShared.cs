using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.LightPaint;

[Serializable, NetSerializable]
public sealed partial class LightPaintDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class LightPaintRemoveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum LightPaintUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class LightPaintColorSelectedMessage(Color color) : BoundUserInterfaceMessage
{
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public enum LightPaintVisuals : byte
{
    Color,
}

[Serializable, NetSerializable]
public enum LightPaintLayers : byte
{
    Paint,
}

/// <summary>Forces the fixture visualizer to refresh its glow layer.</summary>
[Serializable, NetSerializable]
public enum PaintedLightFixtureVisuals : byte
{
    BulbColor,
}
