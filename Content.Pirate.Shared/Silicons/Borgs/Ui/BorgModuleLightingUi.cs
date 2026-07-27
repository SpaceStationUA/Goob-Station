using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Silicons.Borgs;

[Serializable, NetSerializable]
public enum BorgModuleLightingUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed partial class BorgModuleLightingBoundUserInterfaceState : BoundUserInterfaceState
{
    public Color LightColor { get; }
    public bool DiscoMode { get; }
    public float CycleRate { get; }

    public BorgModuleLightingBoundUserInterfaceState(Color lightColor, bool discoMode, float cycleRate)
    {
        LightColor = lightColor;
        DiscoMode = discoMode;
        CycleRate = cycleRate;
    }
}

[Serializable, NetSerializable]
public sealed partial class UpdateBorgModuleLightingMessage : BoundUserInterfaceMessage
{
    public Color LightColor { get; }
    public bool DiscoMode { get; }
    public float CycleRate { get; }

    public UpdateBorgModuleLightingMessage(Color lightColor, bool discoMode, float cycleRate)
    {
        LightColor = lightColor;
        DiscoMode = discoMode;
        CycleRate = cycleRate;
    }
}
