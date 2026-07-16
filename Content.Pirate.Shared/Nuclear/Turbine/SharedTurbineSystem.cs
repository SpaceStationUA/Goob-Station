// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration.Logs;
using Content.Shared.Construction.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.Electrocution;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Repairable;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Pirate.Shared.Nuclear.Turbine;

public abstract partial class SharedTurbineSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private SharedDeviceLinkSystem _device = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private DamageableSystem _damage = default!;
    private EntityQuery<NuclearPropertiesComponent> _propsQuery = default!;

    private const string BladeContainer = "blade_slot";
    private const string StatorContainer = "stator_slot";

    public override void Initialize()
    {
        base.Initialize();

        _propsQuery = GetEntityQuery<NuclearPropertiesComponent>();

        SubscribeLocalEvent<TurbineComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TurbineComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<TurbineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TurbineComponent, RepairFinishedEvent>(OnRepairDoAfter);

        SubscribeLocalEvent<TurbineComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<TurbineComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<TurbineComponent, EntInsertedIntoContainerMessage>(OnPartInserted);
        SubscribeLocalEvent<TurbineComponent, EntRemovedFromContainerMessage>(OnPartEjected);

        SubscribeLocalEvent<TurbineComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<TurbineComponent, PortDisconnectedEvent>(OnPortDisconnected);

        SubscribeLocalEvent<TurbineComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
    }

    private void OnInit(Entity<TurbineComponent> ent, ref ComponentInit args)
    {
        _device.EnsureSourcePorts(ent.Owner, ent.Comp.TurbineDataPort, ent.Comp.SpeedHighPort, ent.Comp.SpeedLowPort);
        _device.EnsureSinkPorts(ent.Owner, ent.Comp.StatorLoadIncreasePort, ent.Comp.StatorLoadDecreasePort);
    }

    private void OnExamined(Entity<TurbineComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (!Transform(ent).Anchored || !args.IsInDetailsRange) // Not anchored? Out of range? No status.
            return;

        using (args.PushGroup(nameof(TurbineComponent)))
        {
            if (comp.CurrentStator == null)
                args.PushMarkup(Loc.GetString("gas-turbine-examine-stator-null"));

            if (comp.CurrentBlade == null)
                args.PushMarkup(Loc.GetString("gas-turbine-examine-blade-null"));
            else
            {
                args.PushMarkup(comp.RPM switch
                {
                    <= 1f => Loc.GetString("gas-turbine-examine-speed-stopped"),
                    <= 60f => Loc.GetString("gas-turbine-examine-speed-slow"),
                    _ when comp.RPM <= comp.BestRPM * 0.5 => Loc.GetString("gas-turbine-examine-speed-normal"),
                    _ when comp.RPM <= comp.BestRPM * 1.2 => Loc.GetString("gas-turbine-examine-speed-fast"),
                    _ => Loc.GetString("gas-turbine-examine-speed-dangerous")
                });
            }

            if (comp.Ruined)
            {
                args.PushMarkup(Loc.GetString("turbine-ruined"));
            }
            else
            {
                var health = (float) comp.BladeHealth / comp.BladeHealthMax;
                args.PushMarkup(health switch
                {
                    < 0.25f => Loc.GetString("turbine-damaged-3"),
                    < 0.5f => Loc.GetString("turbine-damaged-2"),
                    < 0.75f => Loc.GetString("turbine-damaged-1"),
                    _ => Loc.GetString("turbine-damaged-0")
                });
            }
        }
    }

    protected void UpdateAppearance(EntityUid uid, TurbineComponent? comp = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref comp, ref appearance, false))
            return;

        _appearance.SetData(uid, TurbineVisuals.TurbineRuined, comp.Ruined, appearance);

        _appearance.SetData(uid, TurbineVisuals.DamageSpark, comp.IsSparking, appearance);
        _appearance.SetData(uid, TurbineVisuals.DamageSmoke, comp.IsSmoking, appearance);
    }

    #region Repairs
    private void OnInteractUsing(EntityUid uid, TurbineComponent comp, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, comp.RepairTool))
            return;

        args.Handled = true;

        var user = args.User;
        if (comp.CurrentBlade == null)
        {
            Popup.PopupClient(Loc.GetString("gas-turbine-repair-fail-blade"), user, user, PopupType.MediumCaution);
            return;
        }

        if (comp.CurrentStator == null)
        {
            Popup.PopupClient(Loc.GetString("gas-turbine-repair-fail-stator"), user, user, PopupType.MediumCaution);
            return;
        }

        if (comp.BladeHealth >= comp.BladeHealthMax && !comp.Ruined)
        {
            Popup.PopupClient(Loc.GetString("turbine-no-damage", ("target", uid), ("tool", args.Used)), user, user);
            return;
        }

        _tool.UseTool(args.Used, user, uid, comp.RepairDelay, comp.RepairTool, new RepairFinishedEvent(), comp.RepairFuelCost);
    }

    private void OnRepairDoAfter(Entity<TurbineComponent> ent, ref RepairFinishedEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Ruined)
        {
            SetRuined(ent, false);
            if (ent.Comp.BladeHealth <= 0)
            {
                ent.Comp.BladeHealth = 1;
                DirtyField(ent, ent.Comp, nameof(TurbineComponent.BladeHealth));
            }
            UpdateHealthIndicators(ent, args.User);
        }
        else if (ent.Comp.BladeHealth < ent.Comp.BladeHealthMax)
        {
            ent.Comp.BladeHealth++;
            DirtyField(ent, ent.Comp, nameof(TurbineComponent.BladeHealth));
            UpdateHealthIndicators(ent, args.User);
        }

        Popup.PopupClient(Loc.GetString("turbine-repair", ("target", ent), ("tool", args.Used!)), ent, args.User);
        _damage.SetAllDamage(ent.Owner, Comp<DamageableComponent>(ent.Owner), 0);
    }

    private void OnEjectAttempt(EntityUid uid, TurbineComponent comp, ref ItemSlotEjectAttemptEvent args)
    {
        args.Cancelled |= comp.RPM >= 1;
    }

    private void OnInsertAttempt(EntityUid uid, TurbineComponent comp, ref ItemSlotInsertAttemptEvent args)
    {
        args.Cancelled |= comp.RPM >= 1;
    }

    private void OnPartInserted(Entity<TurbineComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        switch (args.Container.ID)
        {
            case BladeContainer:
                ent.Comp.CurrentBlade = args.Entity;
                DirtyField(ent, ent.Comp, nameof(TurbineComponent.CurrentBlade));
                break;
            case StatorContainer:
                ent.Comp.CurrentStator = args.Entity;
                DirtyField(ent, ent.Comp, nameof(TurbineComponent.CurrentStator));
                break;
            default:
                return;
        }
        UpdatePartValues(ent);
    }

    private void OnPartEjected(Entity<TurbineComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        switch (args.Container.ID)
        {
            case BladeContainer:
                ent.Comp.CurrentBlade = null;
                DirtyField(ent, ent.Comp, nameof(TurbineComponent.CurrentBlade));
                break;
            case StatorContainer:
                ent.Comp.CurrentStator = null;
                DirtyField(ent, ent.Comp, nameof(TurbineComponent.CurrentStator));
                break;
            default:
                return;
        }
        UpdatePartValues(ent);
    }

    private void OnSignalReceived(Entity<TurbineComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (args.Port == ent.Comp.StatorLoadIncreasePort)
            ent.Comp.IncreasePortState = state;
        else if (args.Port == ent.Comp.StatorLoadDecreasePort)
            ent.Comp.DecreasePortState = state;
    }

    private void OnPortDisconnected(Entity<TurbineComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port == ent.Comp.StatorLoadIncreasePort)
            ent.Comp.IncreasePortState = SignalState.Low;
        if (args.Port == ent.Comp.StatorLoadDecreasePort)
            ent.Comp.DecreasePortState = SignalState.Low;
    }

    private void OnUnanchorAttempt(Entity<TurbineComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (ent.Comp.RPM < 1)
            return;

        Popup.PopupClient(Loc.GetString("turbine-unanchor-warning"), args.User, args.User, PopupType.LargeCaution);
        args.Cancel();
    }

    protected void UpdatePartValues(Entity<TurbineComponent> ent)
    {
        if (_propsQuery.TryComp(ent.Comp.CurrentBlade, out var blade))
        {
            ent.Comp.TurbineMass = Math.Max(200, 200 * blade.Density);
            ent.Comp.BladeHealthMax = (int)Math.Max(1, 5 * blade.Hardness);
            ent.Comp.BladeHealth = ent.Comp.BladeHealthMax;
            DirtyField(ent, ent.Comp, nameof(TurbineComponent.BladeHealthMax));
            DirtyField(ent, ent.Comp, nameof(TurbineComponent.BladeHealth));
        }

        if (_propsQuery.TryComp(ent.Comp.CurrentStator, out var stator))
        {
            ent.Comp.PowerMultiplier = (float)Math.Max(0.2, 0.2 * stator.ElectricalConductivity);
        }
    }

    protected void UpdateHealthIndicators(Entity<TurbineComponent> ent, EntityUid? user = null)
    {
        var (uid, comp) = ent;
        if (comp.BladeHealth <= 0.75 * comp.BladeHealthMax && !comp.IsSparking)
        {
            comp.IsSparking = true;
            Audio.PlayPredicted(new SoundPathSpecifier("/Audio/Effects/PowerSink/electric.ogg"), uid, user, AudioParams.Default.WithPitchScale(0.75f));
            Popup.PopupPredicted(Loc.GetString("turbine-spark", ("owner", uid)), uid, user, PopupType.MediumCaution);
        }
        else if (comp.BladeHealth > 0.75 * comp.BladeHealthMax && comp.IsSparking)
        {
            comp.IsSparking = false;
            Popup.PopupPredicted(Loc.GetString("turbine-spark-stop", ("owner", uid)), uid, user, PopupType.Medium);
        }

        if (comp.BladeHealth <= 0.5 * comp.BladeHealthMax && !comp.IsSmoking)
        {
            comp.IsSmoking = true;
            Popup.PopupPredicted(Loc.GetString("turbine-smoke", ("owner", uid)), uid, user, PopupType.MediumCaution);
        }
        else if (comp.BladeHealth > 0.5 * comp.BladeHealthMax && comp.IsSmoking)
        {
            comp.IsSmoking = false;
            Popup.PopupPredicted(Loc.GetString("turbine-smoke-stop", ("owner", uid)), uid, user, PopupType.Medium);
        }

        EnsureComp<ElectrifiedComponent>(uid).Enabled = comp.IsSparking;

        UpdateAppearance(uid, comp);
    }

    #endregion

    public bool AdjustStatorLoad(Entity<TurbineComponent> ent, float change)
        => SetStatorLoad(ent, ent.Comp.StatorLoad + change);

    public bool SetStatorLoad(Entity<TurbineComponent> ent, float load)
    {
        if (!float.IsFinite(load))
            return false;

        load = Math.Max(load, ent.Comp.MinStatorLoad);
        if (ent.Comp.StatorLoad == load)
            return false;

        ent.Comp.StatorLoad = load;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.StatorLoad));
        return true;
    }

    public bool SetFlowRate(Entity<TurbineComponent> ent, float rate)
    {
        if (!float.IsFinite(rate))
            return false;

        rate = Math.Clamp(rate, 0, ent.Comp.FlowRateMax);
        if (ent.Comp.FlowRate == rate)
            return false;

        ent.Comp.FlowRate = rate;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.FlowRate));
        return true;
    }

    public void SetRPM(Entity<TurbineComponent> ent, float rpm)
    {
        if (ent.Comp.RPM == rpm)
            return;

        ent.Comp.RPM = rpm;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.RPM));

        // update high/low speed ports if they change
        var high = rpm > ent.Comp.BestRPM * 1.05;
        var low = rpm < ent.Comp.BestRPM * 0.95;
        if (ent.Comp.LastSentHigh != high)
        {
            ent.Comp.LastSentHigh = high;
            _device.SendSignal(ent, ent.Comp.SpeedHighPort, high);
        }
        if (ent.Comp.LastSentLow != low)
        {
            ent.Comp.LastSentLow = low;
            _device.SendSignal(ent, ent.Comp.SpeedLowPort, rpm < ent.Comp.BestRPM * 0.95);
        }
    }

    public void SetLastGen(Entity<TurbineComponent> ent, float value)
    {
        if (ent.Comp.LastGen == value)
            return;

        ent.Comp.LastGen = value;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.LastGen));
    }

    public void SetPowerSupply(Entity<TurbineComponent> ent, float supply)
    {
        if (ent.Comp.PowerSupply == supply)
            return;

        ent.Comp.PowerSupply = supply;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.PowerSupply));
    }

    public void SetRuined(Entity<TurbineComponent> ent, bool ruined = true)
    {
        if (ent.Comp.Ruined == ruined)
            return;

        ent.Comp.Ruined = ruined;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.Ruined));
    }

    public void SetStalling(Entity<TurbineComponent> ent, bool stalling = true)
    {
        if (ent.Comp.Stalling == stalling)
            return;

        ent.Comp.Stalling = stalling;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.Stalling));
    }

    public void SetOverspeed(Entity<TurbineComponent> ent, bool overspeed = true)
    {
        if (ent.Comp.Overspeed == overspeed)
            return;

        ent.Comp.Overspeed = overspeed;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.Overspeed));
    }

    public void SetOvertemp(Entity<TurbineComponent> ent, bool overtemp = true)
    {
        if (ent.Comp.Overtemp == overtemp)
            return;

        ent.Comp.Overtemp = overtemp;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.Overtemp));
    }

    public void SetUndertemp(Entity<TurbineComponent> ent, bool undertemp = true)
    {
        if (ent.Comp.Undertemp == undertemp)
            return;

        ent.Comp.Undertemp = undertemp;
        DirtyField(ent, ent.Comp, nameof(TurbineComponent.Undertemp));
    }
}
