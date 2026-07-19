// SPDX-FileCopyrightText: 2024 Just-a-Unity-Dev <67359748+Just-a-Unity-Dev@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Actions;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared._Pirate.Geras;
using Content.Shared.Zombies;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Geras;

public sealed class GerasSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GerasComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GerasComponent, MorphIntoGerasEvent>(OnMorphIntoGeras);
        SubscribeLocalEvent<GerasComponent, EntityZombifiedEvent>(OnZombified);
    }

    private void OnMapInit(Entity<GerasComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<ZombieComponent>(ent.Owner))
            return;

        AddAction(ent);
    }

    private void OnZombified(Entity<GerasComponent> ent, ref EntityZombifiedEvent args)
    {
        if (ent.Comp.GerasActionEntity is { } action)
            _actions.RemoveAction(action);
    }

    private void AddAction(Entity<GerasComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.GerasActionEntity, ent.Comp.GerasAction);
    }

    private void OnMorphIntoGeras(Entity<GerasComponent> ent, ref MorphIntoGerasEvent args)
    {
        if (args.Handled || HasComp<ZombieComponent>(ent.Owner))
            return;

        if (_polymorph.PolymorphEntity(ent.Owner, ent.Comp.GerasPolymorphId) is not { } geras)
            return;

        _popup.PopupEntity(
            Loc.GetString("geras-popup-morph-message-others", ("entity", geras)),
            geras,
            Filter.PvsExcept(geras),
            true);
        _popup.PopupEntity(Loc.GetString("geras-popup-morph-message-user"), geras, geras);

        args.Handled = true;
    }
}
