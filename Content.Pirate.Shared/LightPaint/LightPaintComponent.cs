using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.LightPaint;

/// <summary>
///     A spray can that tints light bulbs and tubes, changing both the bulb sprite and
///     the colour of the light it casts. Can be used on a loose bulb, or on a light
///     fixture, in which case the bulb installed inside it is painted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightPaintComponent : Component
{
    /// <summary>
    ///     Colour the next sprayed bulb will be tinted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#FFE4CE");

    /// <summary>
    ///     Noise made when paint gets applied.
    /// </summary>
    [DataField]
    public SoundSpecifier Spray = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    /// <summary>
    ///     How long spraying a bulb takes.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Charges spent per bulb painted.
    /// </summary>
    [DataField]
    public int ChargeCost = 1;
}
