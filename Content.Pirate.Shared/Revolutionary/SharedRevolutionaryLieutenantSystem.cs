// SPDX-License-Identifier: MIT

using Content.Pirate.Shared.Revolutionary.Components;
using Content.Shared.Antag;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Pirate.Shared.Revolutionary;

public sealed class SharedRevolutionaryLieutenantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryLieutenantComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
        SubscribeLocalEvent<RevolutionaryLieutenantComponent, ComponentStartup>(DirtyLieutenantComponents);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentStartup>(DirtyLieutenantComponents);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentStartup>(DirtyLieutenantComponents);
        SubscribeLocalEvent<ShowAntagIconsComponent, ComponentStartup>(DirtyLieutenantComponents);
    }

    private void OnGetStateAttempt(
        Entity<RevolutionaryLieutenantComponent> ent,
        ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanSeeLieutenants(args.Player);
    }

    private bool CanSeeLieutenants(ICommonSession? player)
    {
        if (player?.AttachedEntity is not { } uid)
            return true;

        return HasComp<RevolutionaryComponent>(uid)
            || HasComp<HeadRevolutionaryComponent>(uid)
            || HasComp<RevolutionaryLieutenantComponent>(uid)
            || HasComp<ShowAntagIconsComponent>(uid);
    }

    private void DirtyLieutenantComponents<T>(EntityUid uid, T component, ComponentStartup args)
    {
        var query = AllEntityQuery<RevolutionaryLieutenantComponent>();
        while (query.MoveNext(out var lieutenant, out var lieutenantComponent))
        {
            Dirty(lieutenant, lieutenantComponent);
        }
    }
}
