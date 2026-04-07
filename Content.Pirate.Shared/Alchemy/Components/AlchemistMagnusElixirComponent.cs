using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlchemistMagnusElixirComponent : Component
{
    [DataField, AutoNetworkedField]
    public float CollapseKnockdownSeconds = 5f;

    [DataField, AutoNetworkedField]
    public float CollapseParalyzeSeconds = 2f;
}
