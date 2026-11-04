// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Emoting;
using Content.Shared.Body.Events;

namespace Content.Goobstation.Server.Emoting;

public sealed partial class FartSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Pirate: fart emotes and their prototypes are disabled; retain organ state tracking.
        SubscribeLocalEvent<ButtComponent, OrganAddedToBodyEvent>(OnButtAdded);
        SubscribeLocalEvent<ButtComponent, OrganRemovedFromBodyEvent>(OnButtRemoved);
    }

    private void OnButtAdded(Entity<ButtComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!TryComp<FartComponent>(args.Body, out var fart) || !fart.Buttless)
            return;

        fart.Buttless = false;
        Dirty(args.Body, fart);
    }

    private void OnButtRemoved(Entity<ButtComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(args.OldBody) || !TryComp<FartComponent>(args.OldBody, out var fart))
            return;

        fart.Buttless = true;
        fart.Inhaled = false;
        Dirty(args.OldBody, fart);
    }
}
