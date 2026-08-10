// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Index;
using Content.Shared.Administration.Managers;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Index;

/// <summary>
///     Admin interface for the Index: verb to open the admin menu on a pager / Index member,
///     and handling of the admin menu's karma / prescription / fpoon controls.
/// </summary>
public sealed class IndexAdminSystem : EntitySystem
{
    private const int MaxPrescriptionLength = 300;

    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IndexPagerSystem _pager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IndexPagerComponent, GetVerbsEvent<Verb>>(OnPagerVerb);
        SubscribeLocalEvent<IndexMemberComponent, GetVerbsEvent<Verb>>(OnMemberVerb);

        Subs.BuiEvents<IndexPagerComponent>(IndexAdminUiKey.Key, subs =>
        {
            subs.Event<IndexAdminAddKarmaMessage>(OnAddKarma);
            subs.Event<IndexAdminRemoveKarmaMessage>(OnRemoveKarma);
            subs.Event<IndexAdminSendPrescriptionMessage>(OnSendPrescription);
            subs.Event<IndexAdminGuaranteeFpoonMessage>(OnGuaranteeFpoon);
        });
    }

    private static SpriteSpecifier AdminVerbIcon => new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_Pirate/Interface/Misc/index_logo.rsi"), "index_logo");

    private void OnPagerVerb(Entity<IndexPagerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !_admin.IsAdmin(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("index-admin-verb"),
            Icon = AdminVerbIcon,
            Priority = 1,
            Act = () => OpenAdminMenu(ent.Owner, CompOrNull<ActorComponent>(user)?.PlayerSession),
        });
    }

    private void OnMemberVerb(Entity<IndexMemberComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !_admin.IsAdmin(args.User))
            return;

        if (ent.Comp.Pager is not { } pager || !Exists(pager))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("index-admin-verb"),
            Icon = AdminVerbIcon,
            Priority = 1,
            Act = () => OpenAdminMenu(pager, CompOrNull<ActorComponent>(user)?.PlayerSession),
        });
    }

    private void OpenAdminMenu(EntityUid pagerUid, ICommonSession? session)
    {
        if (session == null || !TryComp<IndexPagerComponent>(pagerUid, out var pager))
            return;

        _ui.OpenUi(pagerUid, IndexAdminUiKey.Key, session);
        _pager.UpdateAdminUi((pagerUid, pager));
    }

    #region Admin messages

    private void OnAddKarma(Entity<IndexPagerComponent> ent, ref IndexAdminAddKarmaMessage args)
    {
        if (!_admin.IsAdmin(args.Actor) || !TryGetMember(ent, out var memberUid))
            return;

        _pager.AddKarma(memberUid, args.Amount);
        _pager.UpdateAdminUi(ent);
    }

    private void OnRemoveKarma(Entity<IndexPagerComponent> ent, ref IndexAdminRemoveKarmaMessage args)
    {
        if (!_admin.IsAdmin(args.Actor) || !TryGetMember(ent, out var memberUid))
            return;

        _pager.RemoveKarma(memberUid, args.Amount);
        _pager.UpdateAdminUi(ent);
    }

    private void OnSendPrescription(Entity<IndexPagerComponent> ent, ref IndexAdminSendPrescriptionMessage args)
    {
        if (!_admin.IsAdmin(args.Actor))
            return;

        var text = args.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (text.Length > MaxPrescriptionLength)
            text = text[..MaxPrescriptionLength];

        _pager.SendPrescription(ent.Owner, text);
        _pager.UpdateAdminUi(ent);
    }

    private void OnGuaranteeFpoon(Entity<IndexPagerComponent> ent, ref IndexAdminGuaranteeFpoonMessage args)
    {
        if (!_admin.IsAdmin(args.Actor) || !TryGetMember(ent, out var memberUid))
            return;

        _pager.SetGuaranteeFpoon(memberUid, args.Enabled);
        _pager.UpdateAdminUi(ent);
    }

    private bool TryGetMember(Entity<IndexPagerComponent> ent, out EntityUid memberUid)
    {
        memberUid = default;

        if (ent.Comp.Member is not { } owner || !Exists(owner))
            return false;

        if (!HasComp<IndexMemberComponent>(owner))
            return false;

        memberUid = owner;
        return true;
    }

    #endregion
}
