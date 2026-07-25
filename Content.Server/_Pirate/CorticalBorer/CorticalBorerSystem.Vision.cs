// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Overlays;
using Content.Shared._Pirate.CorticalBorer;

namespace Content.Server._Pirate.CorticalBorer;

public sealed partial class CorticalBorerSystem
{
    private void SubscribeVision()
    {
        SubscribeLocalEvent<NightVisionComponent, SwitchableOverlayToggledEvent>(OnNightVisionToggled);
        SubscribeLocalEvent<ThermalVisionComponent, SwitchableOverlayToggledEvent>(OnThermalVisionToggled);
    }

    private void OnNightVisionToggled(Entity<NightVisionComponent> ent, ref SwitchableOverlayToggledEvent args)
    {
        if (!TryComp<CorticalBorerComponent>(ent, out var borer))
            return;

        SyncHostNightVision((ent.Owner, borer), ent.Comp);
    }

    private void OnThermalVisionToggled(Entity<ThermalVisionComponent> ent, ref SwitchableOverlayToggledEvent args)
    {
        if (!TryComp<CorticalBorerComponent>(ent, out var borer))
            return;

        SyncHostThermalVision((ent.Owner, borer), ent.Comp);
    }

    public void SyncHostVision(Entity<CorticalBorerComponent> borer)
    {
        if (TryComp<NightVisionComponent>(borer, out var nightVision))
            SyncHostNightVision(borer, nightVision);

        if (TryComp<ThermalVisionComponent>(borer, out var thermalVision))
            SyncHostThermalVision(borer, thermalVision);
    }

    private void SyncHostNightVision(Entity<CorticalBorerComponent> borer, NightVisionComponent borerVision)
    {
        if (!TryGetHost(borer, out var host, out var infested))
            return;

        if (!IsVisionActive(borerVision))
        {
            RestoreHostNightVision(host, infested);
            return;
        }

        if (!TryComp<NightVisionComponent>(host, out var hostVision))
        {
            hostVision = EnsureComp<NightVisionComponent>(host);
            infested.AddedBorerNightVision = true;
            ConfigureGrantedVision(host, hostVision, borerVision);
        }
        else if (!infested.AddedBorerNightVision && infested.PreviousHostNightVisionActive is null)
        {
            infested.PreviousHostNightVisionActive = hostVision.IsActive;
        }

        SetVisionActive(host, hostVision, true);
    }

    private void SyncHostThermalVision(Entity<CorticalBorerComponent> borer, ThermalVisionComponent borerVision)
    {
        if (!TryGetHost(borer, out var host, out var infested))
            return;

        if (!IsVisionActive(borerVision))
        {
            RestoreHostThermalVision(host, infested);
            return;
        }

        if (!TryComp<ThermalVisionComponent>(host, out var hostVision))
        {
            hostVision = EnsureComp<ThermalVisionComponent>(host);
            infested.AddedBorerThermalVision = true;
            ConfigureGrantedVision(host, hostVision, borerVision);
        }
        else if (infested.AddedControlThermalVision)
        {
            infested.AddedBorerThermalVision = true;
        }
        else if (!infested.AddedBorerThermalVision &&
                 infested.PreviousHostThermalVisionActive is null)
        {
            infested.PreviousHostThermalVisionActive = hostVision.IsActive;
        }

        SetVisionActive(host, hostVision, true);
    }

    private bool TryGetHost(Entity<CorticalBorerComponent> borer,
        out EntityUid host,
        out CorticalBorerInfestedComponent infested)
    {
        if (borer.Comp.Host is { } currentHost &&
            TryComp<CorticalBorerInfestedComponent>(currentHost, out var currentInfested) &&
            currentInfested.Borer.Owner == borer.Owner)
        {
            host = currentHost;
            infested = currentInfested;
            return true;
        }

        host = default;
        infested = default!;
        return false;
    }

    private static bool IsVisionActive(SwitchableVisionOverlayComponent vision)
    {
        return vision.IsActive || vision.PulseTime > 0f && vision.PulseAccumulator < vision.PulseTime;
    }

    private void ConfigureGrantedVision(EntityUid host,
        SwitchableVisionOverlayComponent hostVision,
        SwitchableVisionOverlayComponent borerVision)
    {
        Actions.RemoveAction(host, hostVision.ToggleActionEntity);
        hostVision.ToggleActionEntity = null;
        hostVision.ToggleAction = null;
        hostVision.IsEquipment = false;
        hostVision.Color = borerVision.Color;
        hostVision.Tint = borerVision.Tint;
        hostVision.Strength = borerVision.Strength;
        hostVision.Noise = borerVision.Noise;
        hostVision.DrawOverlay = borerVision.DrawOverlay;
        hostVision.OverlayOpacity = borerVision.OverlayOpacity;
        hostVision.FlashDurationMultiplier = borerVision.FlashDurationMultiplier;
        hostVision.ActivateSound = null;
        hostVision.DeactivateSound = null;

        if (hostVision is ThermalVisionComponent hostThermal &&
            borerVision is ThermalVisionComponent borerThermal)
        {
            hostThermal.LightRadius = borerThermal.LightRadius;
            hostThermal.ThermalShader = borerThermal.ThermalShader;
        }
    }

    private void SetVisionActive(EntityUid host, SwitchableVisionOverlayComponent vision, bool active)
    {
        vision.IsActive = active;
        if (vision.PulseTime <= 0f)
            Actions.SetToggled(vision.ToggleActionEntity, active);

        Dirty(host, vision);
    }

    private void RestoreHostNightVision(EntityUid host, CorticalBorerInfestedComponent infested)
    {
        if (infested.AddedBorerNightVision)
        {
            infested.AddedBorerNightVision = false;
            RemCompDeferred<NightVisionComponent>(host);
        }
        else if (infested.PreviousHostNightVisionActive is { } wasActive &&
                 TryComp<NightVisionComponent>(host, out var hostVision))
        {
            SetVisionActive(host, hostVision, wasActive);
        }

        infested.PreviousHostNightVisionActive = null;
    }

    private void RestoreHostThermalVision(EntityUid host, CorticalBorerInfestedComponent infested)
    {
        if (infested.AddedBorerThermalVision)
        {
            infested.AddedBorerThermalVision = false;
            if (!infested.AddedControlThermalVision)
                RemCompDeferred<ThermalVisionComponent>(host);
        }
        else if (infested.PreviousHostThermalVisionActive is { } wasActive &&
                 TryComp<ThermalVisionComponent>(host, out var hostVision))
        {
            SetVisionActive(host, hostVision, wasActive);
        }

        infested.PreviousHostThermalVisionActive = null;
    }

    private void ClearHostVision(EntityUid host, CorticalBorerInfestedComponent infested)
    {
        RestoreHostNightVision(host, infested);
        RestoreHostThermalVision(host, infested);
    }
}
