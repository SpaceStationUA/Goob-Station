// SPDX-FileCopyrightText: 2025 Coenx-flex
// SPDX-FileCopyrightText: 2025 Cojoke
// SPDX-FileCopyrightText: 2026 Redrover1760
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._EinsteinEngines.Language;
using Content.Server.Polymorph.Components;
using Content.Server.Radio;
using Content.Shared._DV.Polymorph;
using Content.Shared._Pirate.CorticalBorer;
using Content.Shared.Body.Part;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Polymorph;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.CorticalBorer;

public sealed class CorticalBorerInfestedSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CorticalBorerSystem _borer = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorticalBorerInfestedComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, HeadsetRadioReceiveRelayEvent>(OnRadioMessageHeard);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, BeforePolymorphedEvent>(OnBeforePolymorphed);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, PolymorphedEvent>(OnPolymorphed);
    }

    private void OnMapInit(Entity<CorticalBorerInfestedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ControlContainer = _container.EnsureContainer<Container>(ent, "ControlContainer");
        ent.Comp.InfestationContainer = _container.EnsureContainer<Container>(ent, "InfestationContainer");
    }

    private void OnExamined(Entity<CorticalBorerInfestedComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || args.Examined != args.Examiner || !ent.Comp.Borer.Comp.ControlingHost)
            return;

        if (ent.Comp.ControlTimeEnd is { } controlEnd)
        {
            var timeRemaining = Math.Max(0, Math.Floor((controlEnd - _timing.CurTime).TotalSeconds));
            args.PushMarkup(Loc.GetString("infested-control-examined", ("timeremaining", timeRemaining)));
        }

        args.PushMarkup(Loc.GetString("cortical-borer-self-examine",
            ("chempoints", ent.Comp.Borer.Comp.ChemicalPoints)));
    }

    private void OnMobStateChanged(Entity<CorticalBorerInfestedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && ent.Comp.Borer.Comp.ControlingHost)
            _borer.EndControl(ent.Comp.Borer);
    }

    private void OnBodyPartRemoved(Entity<CorticalBorerInfestedComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (args.Part.Comp.PartType != BodyPartType.Head)
            return;

        _borer.EndControl(ent.Comp.Borer);
        _borer.TryEjectBorer(ent.Comp.Borer);
    }

    private void OnMindRemoved(Entity<CorticalBorerInfestedComponent> ent, ref MindRemovedMessage args)
    {
        if (!ent.Comp.Borer.Comp.ControlingHost ||
            ent.Comp.IsPolymorphing ||
            TryComp<PolymorphedEntityComponent>(ent, out var polymorphed) && polymorphed.Reverted)
        {
            return;
        }

        _borer.EndControl(ent.Comp.Borer);
        _borer.TryEjectBorer(ent.Comp.Borer);
    }

    private void OnRadioMessageHeard(Entity<CorticalBorerInfestedComponent> ent,
        ref HeadsetRadioReceiveRelayEvent args)
    {
        if (TryComp(ent.Comp.Borer, out ActorComponent? actor))
        {
            // Pirate: relay the message variant that the host headset wearer can understand.
            var chat = _language.CanUnderstand(ent.Owner, args.RelayedEvent.Language.ID)
                ? args.RelayedEvent.OriginalChatMsg
                : args.RelayedEvent.LanguageObfuscatedChatMsg;
            var message = new MsgChatMessage { Message = chat };
            _netManager.ServerSendMessage(message, actor.PlayerSession.Channel);
        }
    }

    private void OnBeforePolymorphed(Entity<CorticalBorerInfestedComponent> ent, ref BeforePolymorphedEvent args)
    {
        ent.Comp.IsPolymorphing = true;
    }

    private void OnPolymorphed(Entity<CorticalBorerInfestedComponent> ent, ref PolymorphedEvent args)
    {
        if (ent.Owner != args.OldEntity)
            return;

        var borer = ent.Comp.Borer;
        ent.Comp.IsPolymorphing = false;
        _borer.EndControl(borer, args.NewEntity);
        _borer.TryEjectBorer(borer);
        _borer.InfestTarget(borer, args.NewEntity);
        _borer.SyncHostVision(borer);
    }
}
