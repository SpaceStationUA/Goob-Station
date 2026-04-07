using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared._Shitmed.Humanoid.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using System.Linq;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Shared.Silicons.IPC;

public sealed partial class ScreenSaverActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed class SelectScreenSaverMessage : EntityEventArgs
{
    public string MarkingId { get; }

    public SelectScreenSaverMessage(string markingId)
    {
        MarkingId = markingId;
    }
}

public abstract class SharedScreenSaverSystem : EntitySystem
{
    [Dependency] protected readonly INetManager NetManager = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly MarkingManager MarkingManager = default!;
    [Dependency] protected readonly SharedHumanoidAppearanceSystem Humanoid = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScreenSaverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ScreenSaverComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<HumanoidAppearanceComponent, AfterAutoHandleStateEvent>(OnHumanoidAfterState);
        SubscribeLocalEvent<ScreenSaverComponent, ProfileLoadFinishedEvent>(OnProfileLoadFinished);

        SubscribeLocalEvent<ScreenSaverComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, ScreenSaverComponent component, MobStateChangedEvent args)
    {
        if (component.ActionEntity != null)
            Actions.SetEnabled(component.ActionEntity.Value, args.NewMobState != MobState.Dead);

        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (!NetManager.IsServer)
            return;

        Color? color = null;

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            if (!string.IsNullOrEmpty(component.CurrentScreen))
            {
                if (humanoid.MarkingSet.TryGetMarking(MarkingCategories.Face, component.CurrentScreen, out var marking))
                {
                    if (marking.MarkingColors.Count > 0)
                        color = marking.MarkingColors[0];
                }
                
                humanoid.MarkingSet.Remove(MarkingCategories.Face, component.CurrentScreen);
                Dirty(uid, humanoid);
            }
        }
        
        Humanoid.AddMarking(uid, component.DeathScreen, color, forced: false);
    }

    private void OnStartup(EntityUid uid, ScreenSaverComponent component, ComponentStartup args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnAfterState(EntityUid uid, ScreenSaverComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnHumanoidAfterState(EntityUid uid, HumanoidAppearanceComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<ScreenSaverComponent>(uid, out var screenSaver))
            UpdateVisuals(uid, screenSaver);
    }

    private void OnProfileLoadFinished(EntityUid uid, ScreenSaverComponent component, ProfileLoadFinishedEvent args)
    {
        UpdateVisuals(uid, component);
    }

    public void UpdateVisuals(EntityUid uid, ScreenSaverComponent component)
    {
        // wait for humanoid markings to be ready to avoid incorrect icon initialization.
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) ||
            !humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Face, out var faceMarkings))
        {
            return;
        }

        var lastMarking = faceMarkings.LastOrDefault();
        if (lastMarking == null)
            return;

        var dirty = false;
        if (component.CurrentScreen != lastMarking.MarkingId)
        {
            component.CurrentScreen = lastMarking.MarkingId;
            dirty = true;
        }

        if (dirty && NetManager.IsServer && EntityManager.GetComponent<MetaDataComponent>(uid).EntityLifeStage >= EntityLifeStage.MapInitialized)
            Dirty(uid, component);

        if (component.ActionEntity != null)
            Actions.SetEnabled(component.ActionEntity.Value, _mobState.IsAlive(uid));

        if (component.ActionEntity == null || string.IsNullOrEmpty(component.CurrentScreen))
            return;

        if (!MarkingManager.Markings.TryGetValue(component.CurrentScreen, out var proto) || proto.Sprites.Count == 0)
            return;

        Actions.SetIcon(component.ActionEntity.Value, proto.Sprites[0]);
    }
}
