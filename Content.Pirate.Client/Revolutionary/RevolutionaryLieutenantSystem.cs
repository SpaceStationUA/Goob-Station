// SPDX-License-Identifier: MIT

using Content.Pirate.Shared.Revolutionary.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Revolutionary;

public sealed class RevolutionaryLieutenantSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryLieutenantComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(
        Entity<RevolutionaryLieutenantComponent> ent,
        ref GetStatusIconsEvent args)
    {
        if (_prototype.Resolve(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}
