using Content.Server.Administration;
using Content.Server.Prayer;
using Content.Shared._Pirate.AI;
using Content.Shared.Prayer;
using Robust.Shared.Player;

namespace Content.Server._Pirate.AI;

public sealed class AiRedPhoneActionSystem : EntitySystem
{
    [Dependency] private readonly PrayerSystem _prayer = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AiRedPhoneActionEvent>(OnAiRedPhone);
    }

    private void OnAiRedPhone(AiRedPhoneActionEvent ev)
    {
        if (!TryComp(ev.Performer, out ActorComponent? actor))
        {
            return;
        }

        if (!TryComp(ev.Action, out AiRedPhoneComponent? component))
        {
            Log.Debug("No AiRedPhoneComponent on action entity");
            return;
        }
        
        _quickDialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("ai-redphone-title"), Loc.GetString("ai-redphone-message"),
            message =>
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                var comp = new PrayableComponent
                {
                    SentMessage = component.SentMessage,
                    NotificationPrefix = component.NotificationPrefix,
                    NotificationSound = component.NotificationSound
                };
                
                _prayer.Pray(actor.PlayerSession, comp, message);
            });
    }
}