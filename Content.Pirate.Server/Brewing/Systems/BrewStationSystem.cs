using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Brewing;
using Content.Pirate.Shared.Brewing.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Server.Brewing.Systems;

public sealed class BrewStationSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SolutionContainerMixerSystem _mixers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BrewStationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BrewStationComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BrewStationComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<BrewStationComponent, BrewStationStartMixMessage>(OnStartMixMessage);
        SubscribeLocalEvent<BrewStationComponent, BoundUIOpenedEvent>(UpdateUiState);
        SubscribeLocalEvent<BrewStationComponent, SolutionContainerChangedEvent>(UpdateUiState);
        SubscribeLocalEvent<BrewStationComponent, EntInsertedIntoContainerMessage>(UpdateUiState);
        SubscribeLocalEvent<BrewStationComponent, EntRemovedFromContainerMessage>(UpdateUiState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BrewStationComponent, SolutionContainerMixerComponent>();
        while (query.MoveNext(out var uid, out var station, out var mixer))
        {
            if (station.LastMixingState == mixer.Mixing)
                continue;

            station.LastMixingState = mixer.Mixing;
            UpdateUiState((uid, station));
        }
    }

    private void OnInit(Entity<BrewStationComponent> ent, ref ComponentInit args)
    {
        var listener = EnsureComp<ActiveListenerComponent>(ent);
        listener.Range = ent.Comp.ListenRange;
        ent.Comp.LastMixingState = TryComp<SolutionContainerMixerComponent>(ent, out var mixer) && mixer.Mixing;
    }

    private void OnInteractHand(Entity<BrewStationComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        _ui.OpenUi(ent.Owner, BrewStationUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnListen(Entity<BrewStationComponent> ent, ref ListenEvent args)
    {
        var message = args.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!ent.Comp.VoiceCommands.Any(cmd => message.Contains(cmd, StringComparison.OrdinalIgnoreCase)))
            return;

        if (!TryComp<SolutionContainerMixerComponent>(ent, out var mixer))
            return;

        if (!Transform(ent).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("brew-station-popup-needs-anchor"), ent, args.Source, PopupType.MediumCaution);
            return;
        }

        _mixers.TryStartMix((ent.Owner, mixer), args.Source);
        ent.Comp.LastMixingState = mixer.Mixing;
        UpdateUiState(ent);
    }

    private void OnStartMixMessage(Entity<BrewStationComponent> ent, ref BrewStationStartMixMessage args)
    {
        if (!TryComp<SolutionContainerMixerComponent>(ent, out var mixer))
            return;

        if (!Transform(ent).Anchored)
        {
            _popup.PopupEntity(Loc.GetString("brew-station-popup-needs-anchor"), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        _mixers.TryStartMix((ent.Owner, mixer), args.Actor);
        ent.Comp.LastMixingState = mixer.Mixing;
        UpdateUiState(ent);
    }

    private void UpdateUiState<T>(Entity<BrewStationComponent> ent, ref T args)
    {
        UpdateUiState(ent);
    }

    private void UpdateUiState(Entity<BrewStationComponent> ent)
    {
        NetEntity? containerNet = null;
        string? containerName = null;
        FixedPoint2? volume = null;
        FixedPoint2? maxVolume = null;
        List<ReagentQuantity>? reagents = null;
        var mixing = TryComp<SolutionContainerMixerComponent>(ent, out var mixer) && mixer.Mixing;

        if (mixer != null)
        {
            var inserted = _itemSlots.GetItemOrNull(ent.Owner, mixer.ContainerId);
            if (inserted != null && _solutions.TryGetFitsInDispenser(inserted.Value, out var soln, out var solution))
            {
                containerNet = GetNetEntity(inserted.Value);
                containerName = solution.Name;
                volume = solution.Volume;
                maxVolume = solution.MaxVolume;
                reagents = solution.Contents.ToList();
            }
        }

        var title = MetaData(ent.Owner).EntityName;
        var hint = ent.Comp.VoiceHint;
        if (string.IsNullOrWhiteSpace(hint))
            hint = string.Join(" / ", ent.Comp.VoiceCommands);

        var state = new BrewStationBoundUserInterfaceState(
            title,
            hint,
            containerNet,
            containerName,
            volume,
            maxVolume,
            reagents,
            mixing);

        _ui.SetUiState(ent.Owner, BrewStationUiKey.Key, state);
    }
}
