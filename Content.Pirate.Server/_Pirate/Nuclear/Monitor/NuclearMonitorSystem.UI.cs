// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Nuclear.Monitor;
using Content.Shared.UserInterface;

namespace Content.Pirate.Server.Nuclear.Monitor;

public sealed partial class NuclearMonitorSystem
{
    private void InitializeUi()
    {
        SubscribeLocalEvent<NuclearMonitorComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<NuclearMonitorComponent, BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
    }

    private void OnUiOpenAttempt(Entity<NuclearMonitorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanOpenMonitorUi(ent))
            args.Cancel();
    }

    private void OnUiMessageAttempt(Entity<NuclearMonitorComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Message is OpenBoundInterfaceMessage && !CanOpenMonitorUi(ent))
            args.Cancel();
    }

    private bool CanOpenMonitorUi(Entity<NuclearMonitorComponent> ent)
    {
        // Resolve saved links before checking range.
        if (GetLinked(ent) == null)
            return false;

        CheckRange(ent.AsNullable());
        return GetLinked(ent) != null;
    }
}
