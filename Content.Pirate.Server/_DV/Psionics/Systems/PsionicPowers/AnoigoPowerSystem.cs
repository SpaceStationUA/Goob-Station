using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Popups;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class AnoigoPowerSystem : SharedAnoigoPowerSystem
{
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void OnPowerUsed(Entity<AnoigoPowerComponent> psionic, ref AnoigoPowerActionEvent args)
    {
        var target = args.Target;
        if (!TryComp<DoorComponent>(target, out var door))
        {
            // Not a door - don't consume the cooldown.
            args.Handled = false;
            return;
        }

        if (door.State == DoorState.Welded)
        {
            // Welded - can't be opened, and don't consume the cooldown.
            args.Handled = false;
            return;
        }

        if (TryComp<WiresPanelSecurityComponent>(target, out var wiresPanelSecurity) &&
            !wiresPanelSecurity.WiresAccessible)
        {
            args.Handled = false;
            _popup.PopupEntity(Loc.GetString("airlock-blocked-anoigo-fail"), target, args.Performer, PopupType.MediumCaution);
            return;
        }

        if (TryComp<DoorBoltComponent>(target, out var bolt) && bolt.BoltsDown)
            _door.SetBoltsDown((target, bolt), false, predicted: true);

        if (door.State is not DoorState.Open)
            _door.StartOpening(target, door);

        _audio.PlayEntity("/Audio/_EinsteinEngines/Psionics/wavy.ogg", Filter.Pvs(target), target, true);
        AfterPowerUsed(psionic, args.Performer);
    }
}
