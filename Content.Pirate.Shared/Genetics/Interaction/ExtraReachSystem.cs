// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Interaction;
using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Interaction;

public sealed partial class ExtraReachSystem : EntitySystem
{
    [Dependency] private readonly GeneticsBodySystem _body = default!;

    [SubscribeLocalEvent(before: [typeof(TelekinesisSystem)])]
    private void OnRange(Entity<ExtraReachComponent> ent, ref InRangeOverrideEvent args)
    {
        args.Range += ent.Comp.Bonus;
    }

    [SubscribeLocalEvent(before: [typeof(TelekinesisSystem)])]
    private void OnBodyRange(Entity<MutatableComponent> ent, ref InRangeOverrideEvent args)
    {
        // Pirate: querying enabled parts prevents stale reach after amputation or reattachment.
        foreach (var part in _body.GetBodyParts(ent))
        {
            if (part.Comp.Enabled && TryComp<ExtraReachComponent>(part, out var reach))
                args.Range += reach.Bonus;
        }
    }
}
