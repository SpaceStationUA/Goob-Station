using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.UkraineAlarm;

[Serializable, NetSerializable]
public sealed class UkraineAlarmRegionSelectEuiState : EuiStateBase
{
    public UkraineAlarmRegion[] Regions { get; }

    public UkraineAlarmRegionSelectEuiState(UkraineAlarmRegion[] regions)
    {
        Regions = regions;
    }
}

[Serializable, NetSerializable]
public static class UkraineAlarmRegionSelectEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class SelectRegion : EuiMessageBase
    {
        public string RegionId { get; }

        public SelectRegion(string regionId)
        {
            RegionId = regionId;
        }
    }
}

