using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.BarbellBench;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BarbellLiftComponent : Component
{
    [DataField("staminaCost"), AutoNetworkedField]
    public float StaminaCost = 15f;

    [DataField("emoteLoc"), AutoNetworkedField]
    public string EmoteLoc = "barbell-lift-emote";

    [DataField("emoteLocSelf"), AutoNetworkedField]
    public string EmoteLocSelf = "barbell-lift-emote-self";


    [DataField("overlayRsi"), AutoNetworkedField]
    public ResPath OverlayRsi = new("/Textures/_Pirate/Objects/Fun/barbell.rsi");
}

