using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.LightPaint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightPaintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#FFE4CE");

    [DataField]
    public SoundSpecifier Spray = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public int ChargeCost = 1;
}
