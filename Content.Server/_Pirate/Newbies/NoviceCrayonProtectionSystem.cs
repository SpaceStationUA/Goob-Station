// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Crayon;
using Content.Server.Crayon;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Crayon;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Newbies;

/// <summary>Requires 24 hours of playtime for stock decals, custom symbols and crayon painting.</summary>
public sealed class NoviceCrayonProtectionSystem : EntitySystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(24);

    [Dependency] private readonly NovicePlaytimeSystem _playtime = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextNotification = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrayonComponent, AfterInteractEvent>(OnAfterInteract,
            after: new[] { typeof(IngestionSystem) },
            before: new[] { typeof(CrayonSymbolSystem), typeof(CrayonSystem) });
        SubscribeLocalEvent<CrayonComponent, CrayonDrawImageEvent>(OnDrawImage,
            before: new[] { typeof(CrayonSymbolSystem) });
        SubscribeLocalEvent<CrayonComponent, CrayonDrawAttemptEvent>(OnDrawAttempt);
        SubscribeLocalEvent<ActorComponent, ComponentShutdown>(OnActorShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnActorShutdown(Entity<ActorComponent> ent, ref ComponentShutdown args) => _nextNotification.Remove(ent);

    private void OnRoundRestart(RoundRestartCleanupEvent args) => _nextNotification.Clear();

    private void OnAfterInteract(Entity<CrayonComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && IsRestricted(args.User))
            args.Handled = true;
    }

    private void OnDrawImage(Entity<CrayonComponent> ent, ref CrayonDrawImageEvent args)
    {
        if (!args.Cancelled && IsRestricted(args.User))
            args.Cancelled = true;
    }

    private void OnDrawAttempt(Entity<CrayonComponent> ent, ref CrayonDrawAttemptEvent args)
    {
        if (!args.Cancelled && IsRestricted(args.User))
            args.Cancelled = true;
    }

    private bool IsRestricted(EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor) ||
            !_playtime.IsRestricted(actor.PlayerSession, RequiredPlaytime))
            return false;

        // Paint UIs send many strokes per second: reject every stroke, but throttle only the notification.
        if (!_nextNotification.TryGetValue(user, out var next) || _timing.CurTime >= next)
        {
            _nextNotification[user] = _timing.CurTime + TimeSpan.FromSeconds(2);
            _popup.PopupEntity(Loc.GetString("novice-protection-crayon-drawing"), user, user);
        }
        return true;
    }
}
