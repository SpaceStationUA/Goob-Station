// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Silicons.Laws;
using Content.Shared._Pirate.Antags.SELF;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Tag;

namespace Content.Server._Pirate.Silicons.Laws;

public sealed class FreeMagSystem : EntitySystem
{
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLaws = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconLawProviderComponent, GotEmaggedEvent>(
            OnEmagged,
            after: [typeof(SiliconLawSystem)]);
    }

    private void OnEmagged(Entity<SiliconLawProviderComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction)
            || args.EmagUid is not { } emag
            || !HasComp<FreeMagComponent>(emag)
            || _tags.HasTag(ent.Owner, "StationAi"))
        {
            return;
        }

        var isLawboard = HasComp<FreeMagLawboardComponent>(ent.Owner);
        if (!isLawboard && !HasComp<EmagSiliconLawComponent>(ent.Owner))
            return;

        // Keep the serialized ID and cached lawset in sync for both borgs and uploaded boards.
        ent.Comp.Laws = "FreeLawset";
        ent.Comp.Lawset = _siliconLaws.GetLawset("FreeLawset");

        if (isLawboard)
            _popup.PopupEntity(Loc.GetString("lawboard-emag-popup"), ent.Owner);

        args.Repeatable = isLawboard;
        args.Handled = true;
    }
}
