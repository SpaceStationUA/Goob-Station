// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Paper;
using Content.Shared.UserInterface;

namespace Content.Pirate.Shared.Genetics.Paper;

public sealed partial class BlockReadingSystem : EntitySystem
{
    [Dependency] private EntityQuery<BlockReadingComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<PaperComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (_query.HasComp(args.User))
            args.Cancel();
    }
}
