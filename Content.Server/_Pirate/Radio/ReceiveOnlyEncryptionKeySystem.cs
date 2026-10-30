// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Radio.Components;
using Content.Server.Radio;
using Content.Shared.Radio.Components;

namespace Content.Server._Pirate.Radio;

public sealed class ReceiveOnlyEncryptionKeySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EncryptionKeyHolderComponent, RadioSendAttemptEvent>(OnSendAttempt);
    }

    private void OnSendAttempt(Entity<EncryptionKeyHolderComponent> ent, ref RadioSendAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var receiveOnly = false;
        foreach (var key in ent.Comp.KeyContainer.ContainedEntities)
        {
            if (!TryComp<EncryptionKeyComponent>(key, out var encryption) ||
                !encryption.Channels.Contains(args.Channel.ID))
                continue;

            // A normal key overrides receive-only access to the same channel.
            if (!HasComp<ReceiveOnlyEncryptionKeyComponent>(key))
                return;

            receiveOnly = true;
        }

        if (receiveOnly)
            args.Cancelled = true;
    }
}
