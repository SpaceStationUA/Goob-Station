using Content.Shared._Pirate.CCVar;
using Content.Shared._Pirate.UkraineAlarm;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.UkraineAlarm;

public sealed class UkraineAlarmNotificationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier AlarmStartedSound =
        new SoundPathSpecifier("/Audio/_Pirate/Announcements/Alerts/Air_alerts/air_on.ogg");
    private static readonly SoundSpecifier AlarmEndedSound =
        new SoundPathSpecifier("/Audio/_Pirate/Announcements/Alerts/Air_alerts/air_off.ogg");

    private UkraineAlarmNotificationControl? _notification;
    private TimeSpan _hideAt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<UkraineAlarmNotificationMessage>(OnNotification);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_notification != null && _timing.RealTime >= _hideAt)
        {
            _notification.Orphan();
            _notification = null;
        }
    }

    private void OnNotification(UkraineAlarmNotificationMessage msg)
    {
        if (!_cfg.GetCVar(PirateCVars.UkraineAlarmNotifications))
            return;

        _notification?.Orphan();
        _notification = new UkraineAlarmNotificationControl(msg.Text, msg.DangerLevel);

        _ui.RootControl.AddChild(_notification);
        _audio.PlayGlobal(
            msg.DangerLevel == UkraineAlarmDangerLevel.None ? AlarmEndedSound : AlarmStartedSound,
            Filter.Local(),
            false,
            AudioParams.Default);
        _hideAt = _timing.RealTime + TimeSpan.FromSeconds(12);
    }
}
