// Pirate: ported from ProjectOmu/OmuStation@94c6cb84bac71924f9e114c811a8d1a0ac859a3f.
using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared._Pirate.Xelthia;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chemistry.Components;

namespace Content.Server._Pirate.Xelthia;

/// <summary>
/// Sheds and regrows Xelthia arms at the cost of hunger and thirst.
/// </summary>
public sealed class XelthiaSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XelthiaComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<XelthiaComponent, ArmRegrowthEvent>(OnRegrowthAction);
    }

    private void OnInit(EntityUid uid, XelthiaComponent component, ComponentStartup args)
    {
        _actionsSystem.AddAction(uid, ref component.XelthiaRegenerateAction, out var regenerateAction, "XelthiaRegenerateAction");
        if (regenerateAction?.UseDelay != null)
            component.UseDelay = regenerateAction.UseDelay.Value;
    }

    private void OnRegrowthAction(EntityUid uid, XelthiaComponent component, ArmRegrowthEvent args)
    {
        var start = _gameTiming.CurTime;
        var end = _gameTiming.CurTime + component.UseDelay;
        _actionsSystem.SetCooldown(component.XelthiaRegenerateAction!.Value, start, end); // Cooldown

        var bodySystem = _entityManager.System<BodySystem>();
        var transformSystem = _entityManager.System<SharedTransformSystem>();
        if (!_entityManager.TryGetComponent(uid, out BodyComponent? body)
            || !_entityManager.TryGetComponent(uid, out TransformComponent? xform))
            return;
        if (!bodySystem.TryGetRootPart(uid, out var root, body))
            return;
        // Pirate: snapshot the local body's recursive iterator before detaching its arms.
        var parts = bodySystem.GetBodyChildrenOfType(uid, BodyPartType.Arm, body).ToArray();
        foreach (var part in parts)
        {
            foreach (var child in bodySystem.GetBodyPartChildren(part.Id, part.Component))
                _entityManager.QueueDeleteEntity(child.Id);
            transformSystem.AttachToGridOrMap(part.Id);
            _entityManager.QueueDeleteEntity(part.Id);
            _entityManager.SpawnAtPosition("FoodMeatXelthiaTentacle", xform.Coordinates); // Should drop arms equal to the number attached
        }
        // Right Side
        var newLimb = _entityManager.SpawnAtPosition("RightArmXelthia", xform.Coordinates);
        if (_entityManager.TryGetComponent(newLimb, out BodyPartComponent? limbComp))
            bodySystem.AttachPart(root.Value, "right arm", newLimb, root.Value, limbComp);
        var newerLimb = _entityManager.SpawnAtPosition("RightHandXelthia", xform.Coordinates);
        Enum.TryParse<BodyPartType>("Hand", out var partType);
        bodySystem.TryCreatePartSlotAndAttach(newLimb, "right hand", newerLimb, partType, BodyPartSymmetry.Right);
        // Left Side
        newLimb = _entityManager.SpawnAtPosition("LeftArmXelthia", xform.Coordinates);
        if (_entityManager.TryGetComponent(newLimb, out BodyPartComponent? limbComp2))
            bodySystem.AttachPart(root.Value, "left arm", newLimb, root.Value, limbComp2);
        newerLimb = _entityManager.SpawnAtPosition("LeftHandXelthia", xform.Coordinates);
        bodySystem.TryCreatePartSlotAndAttach(newLimb, "left hand", newerLimb, partType, BodyPartSymmetry.Left);
        _entityManager.EntitySysManager.GetEntitySystem<SharedAudioSystem>()
            .PlayPvs("/Audio/_Pirate/Xelthia/Voice/Xelthia/regrow.ogg", uid, null);

        var solution = new Solution();

        solution.AddReagent("XelthiaArmJuice", 3.5);
        _bloodstreamSystem.TryAddToBloodstream(uid, solution);
    }
}
