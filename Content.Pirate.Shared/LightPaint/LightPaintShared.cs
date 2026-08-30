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

/// <summary>
///     Sent by the colour picker when the player confirms a new colour for the can.
/// </summary>
[Serializable, NetSerializable]
public sealed class LightPaintColorSelectedMessage(Color color) : BoundUserInterfaceMessage
{
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public enum LightPaintVisuals : byte
{
    /// <summary>
    ///     Colour of the paint loaded into the can, used to tint the can's sprite.
    /// </summary>
    Color,
}

[Serializable, NetSerializable]
public enum LightPaintLayers : byte
{
    /// <summary>
    ///     The can sprite layer that gets tinted to the selected colour.
    /// </summary>
    Paint,
}

/// <summary>
///     Set on a light fixture whenever the colour of the bulb inside it changes.
///     <para>
///     This exists purely to force the stock <c>PoweredLightVisualizerSystem</c> to re-run:
///     it refreshes the glow layer from <c>PointLightComponent.Color</c> on any appearance
///     change, but painting a bulb does not change <c>PoweredLightVisuals.BulbState</c>, and
///     <c>SharedAppearanceSystem.SetData</c> ignores writes that don't change the value.
///     </para>
/// </summary>
[Serializable, NetSerializable]
public enum PaintedLightFixtureVisuals : byte
{
    BulbColor,
}
