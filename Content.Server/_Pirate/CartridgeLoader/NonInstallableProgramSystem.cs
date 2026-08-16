// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.CartridgeLoader;
using Content.Shared._Pirate.CartridgeLoader;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.CartridgeLoader;

/// <summary>Blocks installation of slot-only cartridges.</summary>
public sealed class NonInstallableProgramSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProgramInstallationAttempt>(OnInstallationAttempt);
    }

    private void OnInstallationAttempt(ref ProgramInstallationAttempt args)
    {
        // The installation attempt only includes the prototype ID.
        if (!_proto.TryIndex<EntityPrototype>(args.Prototype, out var proto) ||
            !proto.TryGetComponent<NonInstallableProgramComponent>(out _, EntityManager.ComponentFactory))
        {
            return;
        }

        args.Cancelled = true;
    }
}
