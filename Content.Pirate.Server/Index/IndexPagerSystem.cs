// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Index;
using Content.Shared.Audio;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Index;

/// <summary>
///     Handles the Index pager: claiming membership, KARMIC CONSEQUENCE and prescriptions.
/// </summary>
public sealed class IndexPagerSystem : EntitySystem
{
    public const int FpoonKarmaThreshold = 10;

    // The Index shows its face - a fullscreen jumpscare, nothing else.
    // A raw texture, like the Thunderstrike smite, because the RSI loader rejects non-icon-multiple sizes.
    public const string JumpscareImage = "/Textures/_Pirate/Interface/Misc/karmic_consequence.png";

    private static readonly SoundSpecifier BeeperOpen =
        new SoundPathSpecifier("/Audio/_Pirate/Items/Pager/index_beeper_opening.ogg");

    private static readonly SoundSpecifier BeeperClose =
        new SoundPathSpecifier("/Audio/_Pirate/Items/Pager/index_beeper_closing.ogg");

    private static readonly SoundSpecifier BeeperPrescript =
        new SoundPathSpecifier("/Audio/_Pirate/Items/Pager/index_beeper_prescript.ogg");

    private static readonly SoundSpecifier BeeperAlert =
        new SoundPathSpecifier("/Audio/_Pirate/Items/Pager/index_beeper_alert.ogg");

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IndexPagerComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<IndexPagerComponent, BeforeActivatableUIOpenEvent>(OnBeforeOpen);
        SubscribeLocalEvent<IndexPagerComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<IndexPagerComponent, BoundUIClosedEvent>(OnUiClosed);
    }

    private void OnOpenAttempt(Entity<IndexPagerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        // Already bound: only the owner may open the pager.
        if (ent.Comp.Member is { } owner)
        {
            if (owner != args.User)
            {
                if (!args.Silent)
                    _popup.PopupEntity(Loc.GetString("index-pager-not-yours"), ent, args.User, PopupType.Medium);
                args.Cancel();
            }

            return;
        }

        // Unbound: using the pager makes the user an Index member (humanoids only).
        if (!TryClaim(ent, args.User) && !args.Silent)
            _popup.PopupEntity(Loc.GetString("index-pager-claim-fail"), ent, args.User, PopupType.Medium);
    }

    /// <summary>
    ///     Binds the pager to the user and marks them as an Index member.
    ///     Only humanoids may join the Index - no ghosts, no animals.
    /// </summary>
    private bool TryClaim(Entity<IndexPagerComponent> pager, EntityUid user)
    {
        if (!HasComp<HumanoidAppearanceComponent>(user) || HasComp<GhostComponent>(user))
            return false;

        var member = EnsureComp<IndexMemberComponent>(user);
        member.Pager = pager.Owner;
        Dirty(user, member);

        pager.Comp.Member = user;
        pager.Comp.Bound = true;
        Dirty(pager);

        _popup.PopupEntity(Loc.GetString("index-pager-claim-success"), user, user, PopupType.Medium);
        return true;
    }

    private void OnBeforeOpen(Entity<IndexPagerComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdatePagerUi(ent);
    }

    private void OnAfterOpen(Entity<IndexPagerComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        _audio.PlayPvs(BeeperOpen, ent);
    }

    private void OnUiClosed(Entity<IndexPagerComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!Exists(ent) || !Equals(args.UiKey, IndexPagerUiKey.Key))
            return;

        _audio.PlayPvs(BeeperClose, ent);
    }

    /// <summary>Push the latest state to everyone viewing this pager's window.</summary>
    public void UpdatePagerUi(Entity<IndexPagerComponent> pager)
    {
        var owner = pager.Comp.Member;
        var bound = pager.Comp.Bound && owner != null && Exists(owner.Value);
        var memberName = string.Empty;
        var karma = 0;

        if (bound && TryComp<IndexMemberComponent>(owner, out var member))
        {
            memberName = Identity.Name(owner.Value, EntityManager);
            karma = member.KarmicConsequence;
        }

        var state = new IndexPagerBoundUserInterfaceState(karma, pager.Comp.Prescriptions, memberName, bound);
        _ui.SetUiState(pager.Owner, IndexPagerUiKey.Key, state);
    }

    /// <summary>Push the latest state to everyone viewing this pager's admin menu.</summary>
    public void UpdateAdminUi(Entity<IndexPagerComponent> pager)
    {
        var owner = pager.Comp.Member;
        var member = owner != null && Exists(owner.Value) ? CompOrNull<IndexMemberComponent>(owner.Value) : null;
        var hasMember = member != null;
        var name = hasMember ? Identity.Name(owner!.Value, EntityManager) : string.Empty;

        var lastPrescription = pager.Comp.Prescriptions.Count > 0 ? pager.Comp.Prescriptions[^1] : string.Empty;
        var state = new IndexAdminBoundUserInterfaceState(
            member?.KarmicConsequence ?? 0,
            name,
            hasMember,
            member?.NextWeaponFpoon ?? false,
            lastPrescription);

        _ui.SetUiState(pager.Owner, IndexAdminUiKey.Key, state);
    }

    #region KARMIC CONSEQUENCE

    public void AddKarma(EntityUid memberUid, int amount)
    {
        if (!TryComp<IndexMemberComponent>(memberUid, out var member))
            return;

        var old = member.KarmicConsequence;
        member.KarmicConsequence += amount;
        Dirty(memberUid, member);

        // The Index reacts once the threshold is crossed - the fpoon becomes possible.
        if (old < FpoonKarmaThreshold && member.KarmicConsequence >= FpoonKarmaThreshold && member.Pager is { } pager)
        {
            if (Exists(pager))
                _audio.PlayPvs(BeeperAlert, pager);
        }

        PushMemberUi(memberUid, member);
    }

    public void RemoveKarma(EntityUid memberUid, int amount)
    {
        if (!TryComp<IndexMemberComponent>(memberUid, out var member))
            return;

        member.KarmicConsequence = Math.Max(0, member.KarmicConsequence - amount);
        Dirty(memberUid, member);
        PushMemberUi(memberUid, member);
    }

    public void SetGuaranteeFpoon(EntityUid memberUid, bool enabled)
    {
        if (!TryComp<IndexMemberComponent>(memberUid, out var member))
            return;

        member.NextWeaponFpoon = enabled;
        Dirty(memberUid, member);
        PushMemberUi(memberUid, member);
    }

    private void PushMemberUi(EntityUid memberUid, IndexMemberComponent member)
    {
        if (member.Pager is not { } pager || !Exists(pager) || !TryComp<IndexPagerComponent>(pager, out var pagerComp))
            return;

        UpdatePagerUi((pager, pagerComp));
        UpdateAdminUi((pager, pagerComp));
    }

    #endregion

    #region Prescriptions

    /// <summary>
    ///     Send a prescription to the pager. Only the latest prescription is kept - a new one
    ///     replaces the previous. Plays the beeper sound and the receiving animation on the item.
    /// </summary>
    public void SendPrescription(EntityUid pagerUid, string text)
    {
        if (!TryComp<IndexPagerComponent>(pagerUid, out var pager))
            return;

        // Only the last prescription matters - the Index speaks once, and speaks last.
        pager.Prescriptions.Clear();
        pager.Prescriptions.Add(text);

        _audio.PlayPvs(BeeperPrescript, pagerUid);

        _appearance.SetData(pagerUid, IndexPagerVisuals.Receiving, true);
        Timer.Spawn(TimeSpan.FromSeconds(1.5f), () =>
        {
            if (Exists(pagerUid))
                _appearance.SetData(pagerUid, IndexPagerVisuals.Receiving, false);
        });

        UpdatePagerUi((pagerUid, pager));
    }

    #endregion
}
