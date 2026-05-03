using System;
using Content.Server.Body.Systems;
using Content.Server._Pirate.Species.Components;
using Content.Server.Popups;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Pirate.Species.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Species.Systems;

public sealed class HydrakinSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TemperatureSystem _temp = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HydrakinComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HydrakinComponent, HydrakinCoolOffActionEvent>(OnCoolOff);
        SubscribeLocalEvent<HydrakinComponent, CoolOffDoAfterEvent>(OnCoolOffDoAfter);
        SubscribeLocalEvent<HydrakinComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnInit(EntityUid uid, HydrakinComponent component, ComponentInit args)
    {
        if (component.CoolOffAction != null)
            return;

        _actionsSystem.AddAction(uid, ref component.CoolOffAction, component.CoolOffActionId);
    }

    private void OnCoolOff(EntityUid uid, HydrakinComponent component, HydrakinCoolOffActionEvent args)
    {
        var doafter = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(3), new CoolOffDoAfterEvent(), uid);

        if (!_doAfter.TryStartDoAfter(doafter))
            return;

        args.Handled = true;
    }

    private void OnCoolOffDoAfter(Entity<HydrakinComponent> ent, ref CoolOffDoAfterEvent args)
    {
        _popupSystem.PopupEntity(Loc.GetString("hydrakin-cool-off-emote", ("name", Identity.Entity(ent, EntityManager))), ent);
        _audio.PlayEntity(ent.Comp.CoolOffSound, ent, ent);

        if (!TryComp<TemperatureComponent>(ent, out var temperatureComponent))
            return;

        // Heat capacity equation
        // C_h = Q / dT
        // C_h * dT = Q
        //
        // We want to decrease by CoolOffCoefficient % of the current temperature each ability.
        // E.g, if CoolOffCoefficient is 10%, and you are at 255 degrees you should end at 229.5 degrees.
        // Because this doesn't make any real physical sense, we have to do the math backwards to see how many joules
        // we need to take out to get to the new temperature.

        var dT = -(ent.Comp.CoolOffCoefficient * temperatureComponent.CurrentTemperature);
        var C_h = _temp.GetHeatCapacity(ent);
        var Q = C_h * dT;

        _temp.ChangeHeat(ent, Q, true);

        args.Handled = true;
    }

    private void OnPlayerSpawnComplete(EntityUid uid, HydrakinComponent component, PlayerSpawnCompleteEvent args)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        if (!_roleSystem.MindHasRole<JobRoleComponent>(mindId, out var jobRole))
            return;

        var jobId = jobRole.Value.Comp1.JobPrototype;
        if (jobId == null)
            return;

        if (IsJobInDepartment(jobId, "Medical"))
            GiveCyberEyes(uid, "MedicalCyberneticEyes");
        else if (IsJobInDepartment(jobId, "Security"))
            GiveCyberEyes(uid, "SecurityCyberneticEyes");
    }

    private bool IsJobInDepartment(string jobId, string departmentId)
    {
        if (!_prototypeManager.TryIndex<DepartmentPrototype>(departmentId, out var department))
            return false;

        return department.Roles.Contains(jobId);
    }

    private void GiveCyberEyes(EntityUid uid, string eyePrototype)
    {
        foreach (var organ in _bodySystem.GetBodyOrgans(uid))
        {
            if (!EntityManager.TryGetComponent<TransformComponent>(organ.Id, out var xform) || xform.ParentUid == EntityUid.Invalid)
                continue;

            if (!_containerSystem.TryGetContainingContainer((organ.Id, xform, null), out var container))
                continue;

            if (container.ID != SharedBodySystem.GetOrganContainerId("eyes"))
                continue;

            var partId = container.Owner;

            if (!_bodySystem.RemoveOrgan(organ.Id))
                continue;

            EntityManager.DeleteEntity(organ.Id);

            var newEyes = EntityManager.SpawnEntity(eyePrototype, EntityManager.GetComponent<TransformComponent>(uid).Coordinates);
            _bodySystem.InsertOrgan(partId, newEyes, "eyes");
            break;
        }
    }
}