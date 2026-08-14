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
        SubscribeLocalEvent<RevolutionaryLieutenantComponent, ComponentRemove>(DirtyLieutenantComponents);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentRemove>(DirtyLieutenantComponents);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentRemove>(DirtyLieutenantComponents);
        SubscribeLocalEvent<ShowAntagIconsComponent, ComponentRemove>(DirtyLieutenantComponents);
    }

    private void OnGetStateAttempt(
        Entity<RevolutionaryLieutenantComponent> ent,
        ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanSeeLieutenants(args.Player);
    }

    private bool CanSeeLieutenants(ICommonSession? player)
    {
        if (player is null)
            return true; // Match SharedRevolutionarySystem for replay sessions.

        if (player.AttachedEntity is not { } uid)
            return false;

        return HasComp<RevolutionaryComponent>(uid)
            || HasComp<HeadRevolutionaryComponent>(uid)
            || HasComp<RevolutionaryLieutenantComponent>(uid)
            || HasComp<ShowAntagIconsComponent>(uid);
    }

    private void DirtyLieutenantComponents<T>(EntityUid uid, T component, ComponentStartup args)
        => DirtyLieutenantComponents();

    private void DirtyLieutenantComponents<T>(EntityUid uid, T component, ComponentRemove args)
        => DirtyLieutenantComponents();

    public void DirtyLieutenantComponents()
    {
        var query = AllEntityQuery<RevolutionaryLieutenantComponent>();
        while (query.MoveNext(out var lieutenant, out var lieutenantComponent))
        {
            Dirty(lieutenant, lieutenantComponent);
        }
    }
}
