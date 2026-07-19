using Content.Shared._Pirate.BloodBrothers.Components;
using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared._Pirate.BloodBrothers.EntitySystems;

public abstract partial class SharedBloodBrotherSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBloodBrotherComponent, ComponentStartup>(OnInitialBloodBrotherStartup);
        SubscribeLocalEvent<InitialBloodBrotherComponent, ComponentShutdown>(OnInitialBloodBrotherShutdown);
        SubscribeLocalEvent<BloodBrotherComponent, ComponentGetStateAttemptEvent>(OnBloodBrotherAttemptGetState);
    }

    private void OnInitialBloodBrotherStartup(Entity<InitialBloodBrotherComponent> entity, ref ComponentStartup args)
    {
        _actionsSystem.AddAction(entity, ref entity.Comp.ConvertActionEntity, entity.Comp.ConvertAction);
        _actionsSystem.AddAction(entity, ref entity.Comp.CheckConvertActionEntity, entity.Comp.CheckConvertAction);
        Dirty(entity);
    }

    private void OnInitialBloodBrotherShutdown(Entity<InitialBloodBrotherComponent> entity, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(entity.Comp.ConvertActionEntity);
        _actionsSystem.RemoveAction(entity.Comp.CheckConvertActionEntity);
    }

    private void OnBloodBrotherAttemptGetState(
        Entity<BloodBrotherComponent> entity,
        ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(entity, args.Player);
    }

    public void OnBloodBrotherMindshielded(Entity<MindShieldComponent> entity, ref ComponentStartup args)
    {
        if (HasComp<InitialBloodBrotherComponent>(entity))
            return;

        if (!TryComp<BloodBrotherComponent>(entity, out var bloodBrother))
            return;

        var name = Identity.Entity(entity, EntityManager);
        RemCompDeferred<BloodBrotherComponent>(entity);
        if (bloodBrother.DeconversionStunTime != null)
            _stunSystem.TryUpdateParalyzeDuration(entity, bloodBrother.DeconversionStunTime);
        _popupSystem.PopupEntity(
            Loc.GetString("blood-brother-break-control", ("name", name)),
            entity,
            PopupType.MediumCaution);
    }

    private bool CanGetState(Entity<BloodBrotherComponent> entity, ICommonSession? player)
    {
        //Apparently this can be null in replays so I am just returning true.
        if (player?.AttachedEntity is not {} uid)
            return true;

        return uid == entity.Owner
            || uid == entity.Comp.Brother
            || HasComp<ShowAntagIconsComponent>(uid);
    }
}
