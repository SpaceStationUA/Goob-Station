// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio.Components;
using Content.Shared.UserInterface;

namespace Content.Shared._Pirate.NanoChatMonitor;

/// <summary>
///     Runs on both sides so the client only offers the viewer when the server can open it.
/// </summary>
public sealed class SharedNanoChatLogHostSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoChatLogHostComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    public bool HostsLog(Entity<NanoChatLogHostComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!ent.Comp.RequiresKey)
            return true;

        return TryComp<EncryptionKeyHolderComponent>(ent, out var keys) && keys.Channels.Contains(ent.Comp.Channel);
    }

    private void OnOpenAttempt(Entity<NanoChatLogHostComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HostsLog((ent.Owner, ent.Comp)))
            args.Cancel();
    }
}
