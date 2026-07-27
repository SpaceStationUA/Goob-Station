using Content.Shared.PAI;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.PAI;

/// <summary>
/// Syncs EncryptionKeyHolder channels to ActiveRadio for pAIs.
/// Stores the YAML-default channels on MapInit so they are never lost.
/// </summary>
[RegisterComponent]
public sealed partial class PAIKeyStateComponent : Component
{
    public HashSet<ProtoId<RadioChannelPrototype>> DefaultChannels = new();
}

public sealed class PAIEncryptionKeySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PAIComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PAIComponent, EncryptionChannelsChangedEvent>(OnChannelsChanged);
    }

    private void OnMapInit(Entity<PAIComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ActiveRadioComponent>(ent, out var radio))
            return;

        var state = EnsureComp<PAIKeyStateComponent>(ent);
        state.DefaultChannels = new(radio.Channels);
        SyncRadio(ent, state, radio);
    }

    private void OnChannelsChanged(Entity<PAIComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        if (!TryComp<ActiveRadioComponent>(ent, out var radio)
            || !TryComp<PAIKeyStateComponent>(ent, out var state))
            return;

        SyncRadio(ent, state, radio);
    }

    private void SyncRadio(EntityUid uid, PAIKeyStateComponent state, ActiveRadioComponent radio)
    {
        radio.Channels.Clear();
        foreach (var channel in state.DefaultChannels)
            radio.Channels.Add(channel);

        if (TryComp<EncryptionKeyHolderComponent>(uid, out var keys))
        {
            foreach (var channel in keys.Channels)
            {
                if (!radio.Channels.Contains(channel))
                    radio.Channels.Add(channel);
            }
        }

        Dirty(uid, radio);
    }
}
