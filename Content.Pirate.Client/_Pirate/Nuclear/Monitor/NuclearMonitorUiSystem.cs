// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Nuclear.Monitor;
using Content.Shared.UserInterface;

namespace Content.Pirate.Client.Nuclear.Monitor;

// The server validates the linked machine and range.
public sealed class NuclearMonitorUiSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NuclearMonitorComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<NuclearMonitorComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
    }

    private void OnUiOpenAttempt(Entity<NuclearMonitorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.Linked == null)
            args.Cancel();
    }

    private void OnUiMessageAttempt(Entity<NuclearMonitorComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Message is OpenBoundInterfaceMessage && ent.Comp.Linked == null)
            args.Cancel();
    }
}
