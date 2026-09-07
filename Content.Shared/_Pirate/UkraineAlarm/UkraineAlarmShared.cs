using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.UkraineAlarm;

[Serializable, NetSerializable]
public enum UkraineAlarmDangerLevel : byte
{
    None,
    Yellow,
    Red
}

[Serializable, NetSerializable]
public sealed class UkraineAlarmRegion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public UkraineAlarmRegion()
    {
    }

    public UkraineAlarmRegion(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class UkraineAlarmNotificationMessage : EntityEventArgs
{
    public string Text { get; }
    public UkraineAlarmDangerLevel DangerLevel { get; }

    public UkraineAlarmNotificationMessage(string text, UkraineAlarmDangerLevel dangerLevel)
    {
        Text = text;
        DangerLevel = dangerLevel;
    }
}


