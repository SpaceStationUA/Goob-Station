using System;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class LightReactiveComponent : Component
{
    [DataField("range")]
    public float Range = 2.5f;

    [DataField("requiredDot")]
    public float RequiredDot = 0.82f;

    [DataField("updateInterval")]
    public float UpdateInterval = 0.1f;

    [DataField("requiredColor")]
    public Color RequiredColor = Color.FromHex("#9f66ff");

    [DataField("colorTolerance")]
    public float ColorTolerance = 0.12f;

    [DataField("onPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string OnPort = "On";

    [DataField("offPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string OffPort = "Off";

    [DataField("reactSound")]
    public SoundSpecifier? ReactSound;

    public TimeSpan NextUpdate;
    public bool Active;
}
