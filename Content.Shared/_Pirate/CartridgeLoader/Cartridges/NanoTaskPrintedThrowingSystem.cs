// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.Throwing;

namespace Content.Shared._Pirate.CartridgeLoader.Cartridges;

public sealed class NanoTaskPrintedThrowingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoTaskPrintedComponent, ThrowPushbackAttemptEvent>(OnThrowPushbackAttempt);
    }

    private void OnThrowPushbackAttempt(
        EntityUid uid,
        NanoTaskPrintedComponent component,
        ThrowPushbackAttemptEvent args)
    {
        args.Cancel();
    }
}
