using System;
using System.Linq;
using System.Collections.Generic;
using Robust.Shared.Audio.Systems;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Server.Containers;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Movement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Ghost;
using Content.Shared.Emoting;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Shitmed.Body.Components;
using Content.Shared._RMC14.GhostColor;
using Content.Shared.Popups;
using Content.Shared.Damage.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Examine;
using Content.Server.Mind;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Server.Zombies;
using Content.Shared.Zombies;
using Content.Server.Atmos.Components;
using Content.Server.Speech.Components;
using Content.Server.Actions;
using Content.Server._EinsteinEngines.Language;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Goobstation.Shared.Religion;
using Content.Goobstation.Common.CrematorImmune;
using Content.Pirate.Shared.Components;
using Content.Pirate.Shared;

namespace Content.Pirate.Server.Systems
{
    public sealed class GhostTargetingSystem : EntitySystem
    {
        [Dependency] private readonly SharedMindSystem _mind = default!;
        [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;
        [Dependency] private readonly ContainerSystem _containers = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GhostTargetingComponent, MapInitEvent>(OnStartup);
            SubscribeLocalEvent<GhostTargetingComponent, ComponentRemove>(OnRemove);
            SubscribeLocalEvent<GhostTargetingComponent, ClearTargetGhostActionEvent>(OnClearTargetGhost);
            SubscribeLocalEvent<GhostTargetingComponent, SetTargetGhostActionEvent>(OnSetTargetGhost);
            SubscribeLocalEvent<GhostTargetingComponent, RelinquishBodyControlEvent>(OnRelinquishSpirit);
            SubscribeLocalEvent<GhostPossessedComponent, RelinquishBodyControlEvent>(OnRelinquishBody);
            SubscribeLocalEvent<GhostPossessedComponent, EntityTerminatingEvent>(OnPossessedBodyTerminating, before: [typeof(MindSystem)]);
            SubscribeLocalEvent<GhostTargetingComponent, EntityTerminatingEvent>(OnSpiritTerminating, before: [typeof(MindSystem)]);
            SubscribeLocalEvent<GhostTargetingComponent, ToggleGhostFormActionEvent>(OnToggleGhostForm);
            SubscribeLocalEvent<GhostTargetingComponent, GhostBlinkActionEvent>(OnGhostBlinkAction);
            SubscribeLocalEvent<GhostTargetingComponent, TakeControlGhostActionEvent>(OnTakeControlGhost);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var component in EntityManager.EntityQuery<GhostTargetingComponent>())
            {
                if (component.ControlledTarget is not { } target)
                    continue;

                var targetDeleted = TerminatingOrDeleted(target);
                var targetDead = !targetDeleted &&
                                 TryComp<MobStateComponent>(target, out var mobState) &&
                                 mobState.CurrentState == MobState.Dead;

                if (targetDeleted || targetDead || _timing.CurTime >= component.ControlTimeEnd)
                    EndControl(component.Owner, component);
            }
        }

        private void OnGhostBlinkAction(EntityUid uid, GhostTargetingComponent comp, GhostBlinkActionEvent args)
        {
            var entityManager = EntityManager;
            if (!comp.IsGhost)
                return;
            if (!entityManager.TryGetComponent(uid, out TransformComponent? xform) || xform == null)
                return;
            var direction = xform.LocalRotation.ToWorldVec();
            var distance = 4f;
            var oldPos = xform.Coordinates;
            var newPos = oldPos.Offset(direction * distance);

            var effectProto = "SwapSpellEffect";
            var soundProto = new SoundPathSpecifier("/Audio/_Goobstation/Wizard/swap.ogg");
            var audioSys = entityManager.System<SharedAudioSystem>();

            EntityUid? soundTarget = null;
            if (comp.Target != NetEntity.Invalid)
            {
                var selectedTarget = entityManager.GetEntity(comp.Target);
                if (entityManager.EntityExists(selectedTarget) &&
                    entityManager.HasComponent<ActorComponent>(selectedTarget))
                {
                    soundTarget = selectedTarget;
                }
            }

            void SpawnSwapEffect(EntityCoordinates coords)
            {
                var effect = entityManager.SpawnEntity(effectProto, coords);

                if (soundTarget is { } target)
                    audioSys.PlayEntity(soundProto, target, effect);

                // Ensure VisibilityComponent exists
                var visComp = entityManager.EnsureComponent<VisibilityComponent>(effect);
                var visSys = entityManager.System<VisibilitySystem>();
                visSys.AddLayer((effect, visComp), (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost, false);
                visSys.RefreshVisibility(effect, visibilityComponent: visComp);

                if (entityManager.TryGetComponent<TrailComponent>(effect, out var trail))
                {
                    var transformSystem = entityManager.System<SharedTransformSystem>();
                    trail.SpawnPosition = coords.ToMap(entityManager, transformSystem).Position;
                    trail.RenderedEntity = uid;
                    Dirty(effect, trail);
                }
            }

            SpawnSwapEffect(oldPos);
            SpawnSwapEffect(newPos);

            xform.Coordinates = newPos;
        }

        private void OnStartup(Entity<GhostTargetingComponent> ghost, ref MapInitEvent args)
        {
            // Додаємо базові action-прототипи
            if (ghost.Comp.ActionEntities == null)
                ghost.Comp.ActionEntities = new List<EntityUid>();

            foreach (var actionId in ghost.Comp.BaseGhostActions)
            {
                var ent = EntityManager.System<ActionsSystem>().AddAction(ghost, actionId);
                if (ent != null)
                {
                    ghost.Comp.ActionEntities.Add(ent.Value);
                    if (actionId == "ActionJustDecorToggleGhostForm")
                        ghost.Comp.ToggleGhostFormActionUid = ent.Value;
                }
            }

            if (TryGetEntity(ghost.Comp.Target, out var target))
                GrantTargetSight(ghost.Owner, target.Value);
        }

        private const int SpiritLayer = (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;

        private void GrantTargetSight(EntityUid spirit, EntityUid target)
        {
            if (!TryComp<EyeComponent>(target, out var eye))
                return;

            var viewer = EnsureComp<GhostTargetViewerComponent>(target);
            if (!viewer.Spirits.Add(spirit) || viewer.Spirits.Count != 1)
                return;

            viewer.HadLayer = (eye.VisibilityMask & SpiritLayer) != 0;
            EntityManager.System<EyeSystem>().SetVisibilityMask(target, eye.VisibilityMask | SpiritLayer, eye);
        }

        private void RevokeTargetSight(EntityUid spirit, GhostTargetingComponent form)
        {
            if (!TryGetEntity(form.Target, out var target) || !TryComp<GhostTargetViewerComponent>(target, out var viewer) ||
                !viewer.Spirits.Remove(spirit) || viewer.Spirits.Count != 0)
                return;

            if (!viewer.HadLayer && TryComp<EyeComponent>(target, out var eye))
                EntityManager.System<EyeSystem>().SetVisibilityMask(target.Value, eye.VisibilityMask & ~SpiritLayer, eye);
            RemComp<GhostTargetViewerComponent>(target.Value);
        }

        private void OnRemove(Entity<GhostTargetingComponent> ghost, ref ComponentRemove args)
        {
            if (ghost.Comp.ControlledTarget is not null)
                EndControl(ghost.Owner, ghost.Comp);

            RevokeTargetSight(ghost.Owner, ghost.Comp);
            if (TerminatingOrDeleted(ghost.Owner))
                return;

            // Відновлюємо стару форму, якщо entity був у ghost-формі
            if (ghost.Comp.SavedState?.VisibilityLayers.HasValue == true)
            {
                var uid = ghost.Owner;
                var visSys = EntityManager.System<VisibilitySystem>();
                RemoveImmunities(uid, ghost.Comp);

                // Eye
                if (EntityManager.TryGetComponent<EyeComponent>(uid, out var eyeNormal) && eyeNormal != null)
                {
                    var eyeSys = EntityManager.System<EyeSystem>();
                    // Прибираємо TargetingGhost layer з EyeComponent
                    var mask = eyeNormal.VisibilityMask & ~(int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
                    if (ghost.Comp.SavedState.VisibilityMask.HasValue)
                        mask = ghost.Comp.SavedState.VisibilityMask.Value &
                               ~(int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
                    eyeSys.SetVisibilityMask(uid, mask, eyeNormal);
                    if (ghost.Comp.SavedState.DrawFov.HasValue)
                        eyeSys.SetDrawFov(uid, ghost.Comp.SavedState.DrawFov.Value, eyeNormal);
                }

                // Physics
                if (EntityManager.TryGetComponent(uid, out PhysicsComponent? physics) && physics != null)
                {
                    var physicsSys = EntityManager.System<SharedPhysicsSystem>();
                    if (ghost.Comp.SavedState.CanCollide.HasValue)
                        physicsSys.SetCanCollide(uid, ghost.Comp.SavedState.CanCollide.Value, body: physics);
                    if (ghost.Comp.SavedState.BodyType.HasValue)
                        physicsSys.SetBodyType(uid, ghost.Comp.SavedState.BodyType.Value, body: physics);
                }

                visSys.SetLayer(uid, (ushort) ghost.Comp.SavedState.VisibilityLayers.Value);
                RemoveGhostComponents(uid, ghost.Comp);
            }

            // Видаляємо всі action entity, якщо вони були додані
            if (ghost.Comp.ActionEntities != null)
            {
                var actionsSystem = EntityManager.System<ActionsSystem>();
                if (TryComp<ActionsComponent>(ghost, out var actionsComp))
                {
                    foreach (var ent in ghost.Comp.ActionEntities)
                    {
                        actionsSystem.RemoveAction((ghost, actionsComp), ent);
                    }
                }

                ghost.Comp.ActionEntities.Clear();
            }
        }

        public void OnClearTargetGhost(EntityUid uid, GhostTargetingComponent comp, ClearTargetGhostActionEvent args)
        {
            if (TryGetEntity(comp.Target, out var target))
                EntityManager.System<SharedPopupSystem>().PopupEntity($"Ціль '{Name(target.Value)}' скинута!", uid, uid, PopupType.Medium);

            RevokeTargetSight(uid, comp);
            comp.Target = NetEntity.Invalid;
            args.Handled = true;
        }

        public void OnSetTargetGhost(EntityUid uid, GhostTargetingComponent comp, SetTargetGhostActionEvent args)
        {
            if (args.Target == uid || !EntityManager.EntityExists(args.Target))
                return;

            var popup = EntityManager.System<SharedPopupSystem>();
            if (TryGetEntity(comp.Target, out var previous) && previous != args.Target)
            {
                RevokeTargetSight(uid, comp);
                popup.PopupEntity($"Стара ціль '{Name(previous.Value)}' очищена!", uid, uid, PopupType.Small);
            }

            comp.Target = GetNetEntity(args.Target);
            GrantTargetSight(uid, args.Target);
            popup.PopupEntity($"Ціль встановлено: '{Name(args.Target)}'!", uid, uid, PopupType.Medium);
            args.Handled = true;
        }

        public void OnToggleGhostForm(EntityUid uid, GhostTargetingComponent comp, ToggleGhostFormActionEvent args)
        {
            var popupSys = EntityManager.System<SharedPopupSystem>();
            var actionEntity = comp.ToggleGhostFormActionUid ?? args.Action;
            if (!comp.IsGhost)
            {
                SaveState(uid, comp);
                SaveImmunities(uid, comp);
                AddGhostComponents(uid, comp);
                AddImmunities(uid, comp);
                SetupEyePhysicsVisibility(uid, comp, ghostMode: true);
                comp.IsGhost = true;
                Dirty(uid, comp);

                if (EntityManager.TryGetComponent<ActionComponent>(actionEntity, out var actionComp))
                {
                    EntityManager.System<SharedActionsSystem>()
                        .SetIcon(actionEntity,
                            new Robust.Shared.Utility.SpriteSpecifier.Texture(
                                new ResPath("Interface/Actions/eyeclose.png")));
                }

                popupSys.PopupEntity("Ви перетворилися на привида!", uid, uid, PopupType.Medium);
            }
            else
            {
                RemoveGhostComponents(uid, comp);
                RemoveImmunities(uid, comp);
                SetupEyePhysicsVisibility(uid, comp, ghostMode: false);
                ClearSavedState(comp);
                comp.IsGhost = false;
                Dirty(uid, comp);

                if (EntityManager.TryGetComponent<ActionComponent>(actionEntity, out var actionComp))
                {
                    EntityManager.System<SharedActionsSystem>()
                        .SetIcon(actionEntity,
                            new Robust.Shared.Utility.SpriteSpecifier.Texture(
                                new ResPath("Interface/Actions/eyeopen.png")));
                }

                popupSys.PopupEntity("Ви повернули свою стару форму!", uid, uid, PopupType.Medium);
            }
        }

        private void OnTakeControlGhost(EntityUid uid, GhostTargetingComponent comp, TakeControlGhostActionEvent args)
        {
            var popupSys = EntityManager.System<SharedPopupSystem>();
            if (!comp.IsGhost || comp.ControlledTarget is not null || HasComp<GhostPossessedComponent>(uid))
                return;

            if (_timing.CurTime < comp.ControlCooldownEnd)
            {
                var remaining = Math.Ceiling((comp.ControlCooldownEnd - _timing.CurTime).TotalSeconds);
                popupSys.PopupEntity($"Здатність відновиться через {remaining:0} с.", uid, uid, PopupType.Medium);
                return;
            }

            if (comp.Target == NetEntity.Invalid)
            {
                popupSys.PopupEntity("Спершу оберіть тіло для контролю.", uid, uid, PopupType.Medium);
                return;
            }

            var target = EntityManager.GetEntity(comp.Target);
            if (!EntityManager.EntityExists(target) || target == uid ||
                !TryComp<MobStateComponent>(target, out var targetState) ||
                targetState.CurrentState == MobState.Dead)
            {
                popupSys.PopupEntity("Обрана ціль не є живим тілом.", uid, uid, PopupType.Medium);
                return;
            }

            if (!_mind.TryGetMind(uid, out var ghostMind, out var ghostMindComp))
            {
                popupSys.PopupEntity("Для контролю потрібна свідомість у вашому тілі.", uid, uid, PopupType.Medium);
                return;
            }

            // Relinquish an earlier possession before taking a snapshot of the body's owner.
            var relinquish = new RelinquishBodyControlEvent();
            RaiseLocalEvent(target, ref relinquish);
            var hasOriginalMind = _mind.TryGetMind(target, out var targetMind, out var targetMindComp);
            if (TryComp<VisitingMindComponent>(target, out var visitor) && visitor.MindId != null ||
                !hasOriginalMind && HasComp<ActorComponent>(target))
            {
                popupSys.PopupEntity("Не вдалося безпечно відокремити свідомість цілі.", uid, uid, PopupType.Medium);
                return;
            }

            comp.ControlContainer ??= _containers.EnsureContainer<Container>(uid, "GhostControlContainer");
            var mindHolder = Spawn(prototype: null, coordinates: MapCoordinates.Nullspace);
            EnsureComp<MindContainerComponent>(mindHolder);

            if (!_containers.Insert(mindHolder, comp.ControlContainer))
            {
                Del(mindHolder);
                popupSys.PopupEntity("Не вдалося підготувати тіло для контролю.", uid, uid, PopupType.Medium);
                return;
            }

            comp.ControlledTarget = target;
            comp.ControlledMind = ghostMind;
            comp.OriginalTargetMind = hasOriginalMind ? targetMind : null;
            comp.ControlMindHolder = mindHolder;
            comp.WasVisitingGhost = ghostMindComp.VisitingEntity == uid;
            var possession = EnsureComp<GhostPossessedComponent>(target);
            possession.Spirit = uid;
            if (TryComp<GhostRoleComponent>(target, out var targetRole))
            {
                possession.RoleWasTaken = targetRole.Taken;
                possession.RoleWasListed = _ghostRoles.GhostRoles.Any(role => role.Owner == target);
            }

            if (hasOriginalMind)
                _mind.TransferTo(targetMind, mindHolder, mind: targetMindComp);

            if (comp.WasVisitingGhost)
            {
                // An aghost is a visit. Keep the admin's mind attached to their original body.
                _mind.UnVisit(ghostMind, ghostMindComp);
                EnsureComp<MindContainerComponent>(target);
                Visit(ghostMind, target, ghostMindComp);
            }
            else
                _mind.TransferTo(ghostMind, target);
            // Visit does not fire MindAddedMessage. Reserve the role even for a visiting aghost.
            if (targetRole != null)
            {
                _ghostRoles.SetTaken(targetRole, true);
                _ghostRoles.UnregisterGhostRole((target, targetRole));
            }

            comp.ControlTimeEnd = _timing.CurTime + comp.ControlDuration;
            comp.ControlCooldownEnd = _timing.CurTime + comp.ControlCooldown;

            if (TryComp<GhostRoleComponent>(uid, out var ghostRole))
                _ghostRoles.UnregisterGhostRole((uid, ghostRole));

            args.Handled = true;
            popupSys.PopupEntity("Ви тимчасово заволоділи тілом цілі.", target, target, PopupType.Medium);
        }

        private void OnRelinquishSpirit(Entity<GhostTargetingComponent> ghost, ref RelinquishBodyControlEvent args)
        {
            EndControl(ghost.Owner, ghost.Comp);
        }

        private void OnRelinquishBody(Entity<GhostPossessedComponent> body, ref RelinquishBodyControlEvent args)
        {
            if (TryComp<GhostTargetingComponent>(body.Comp.Spirit, out var ghost) && ghost.ControlledTarget == body.Owner)
                EndControl(body.Comp.Spirit, ghost);
        }

        private void OnPossessedBodyTerminating(Entity<GhostPossessedComponent> body, ref EntityTerminatingEvent args)
        {
            if (TryComp<GhostTargetingComponent>(body.Comp.Spirit, out var ghost) && ghost.ControlledTarget == body.Owner)
                EndControl(body.Comp.Spirit, ghost);
        }

        private void OnSpiritTerminating(Entity<GhostTargetingComponent> ghost, ref EntityTerminatingEvent args)
        {
            EndControl(ghost.Owner, ghost.Comp);
        }

        private void Visit(EntityUid mindId, EntityUid body, MindComponent mind)
        {
            // UnVisit defers removal. A second spirit may take the same body in this tick.
            if (TryComp<VisitingMindComponent>(body, out var visit) && visit.MindId == null)
                RemComp<VisitingMindComponent>(body);

            _mind.Visit(mindId, body, mind);
        }

        private void EndControl(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.ControlledTarget is not { } target)
                return;

            var sourceExists = !TerminatingOrDeleted(uid);
            var targetExists = !TerminatingOrDeleted(target);

            var possession = CompOrNull<GhostPossessedComponent>(target);
            if (targetExists)
                RemComp<GhostPossessedComponent>(target);

            if (comp.ControlledMind is { } controlledMind && TryComp<MindComponent>(controlledMind, out var mind))
            {
                // Deleting the admin's original body promotes the visited target to their owned body.
                if (mind.OwnedEntity == target)
                    _mind.TransferTo(controlledMind, sourceExists ? uid : null, ghostCheckOverride: true, mind: mind);
                else if (comp.WasVisitingGhost && mind.VisitingEntity == target)
                {
                    _mind.UnVisit(controlledMind, mind);
                    if (sourceExists && !_mind.TryGetMind(uid, out _, out _))
                        Visit(controlledMind, uid, mind);
                }
                else if (!targetExists && mind.OwnedEntity == null)
                    _mind.TransferTo(controlledMind, sourceExists ? uid : null, mind: mind);
            }

            if (comp.OriginalTargetMind is { } originalMind && TryComp<MindComponent>(originalMind, out var owner) &&
                owner.OwnedEntity == comp.ControlMindHolder)
            {
                var bodyAvailable = targetExists && !_mind.TryGetMind(target, out _, out _);
                _mind.TransferTo(originalMind, bodyAvailable ? target : null, mind: owner);
            }

            if (targetExists && possession?.RoleWasTaken is { } wasTaken &&
                TryComp<GhostRoleComponent>(target, out var role))
            {
                if (!_mind.TryGetMind(target, out _, out _))
                {
                    _ghostRoles.SetTaken(role, wasTaken);
                    if (possession.RoleWasListed)
                        _ghostRoles.RegisterGhostRole((target, role));
                    else
                        _ghostRoles.UnregisterGhostRole((target, role));
                }
            }

            if (comp.ControlContainer != null)
                _containers.CleanContainer(comp.ControlContainer);

            comp.ControlledTarget = null;
            comp.ControlledMind = null;
            comp.OriginalTargetMind = null;
            comp.ControlMindHolder = null;
            comp.WasVisitingGhost = false;
            comp.ControlTimeEnd = TimeSpan.Zero;

            if (!sourceExists)
                return;

            // An external mind transfer can leave behind the observer kept alive during possession.
            if (HasComp<GhostComponent>(uid) && !HasComp<ActorComponent>(uid) && !_mind.TryGetMind(uid, out _, out _))
                QueueDel(uid);
            else
                EntityManager.System<SharedPopupSystem>().PopupEntity("Контроль над тілом завершився.", uid, uid, PopupType.Medium);
        }

        //  Приватні методи для логіки
        private void SaveState(EntityUid uid, GhostTargetingComponent comp)
        {
            comp.SavedState = new GhostStateSnapshot();

            // Додаткові компоненти
            comp.SavedState.HadFTLSmashImmune = EntityManager.HasComponent<FTLSmashImmuneComponent>(uid);
            comp.SavedState.HadUniversalLanguageSpeaker = EntityManager.HasComponent<UniversalLanguageSpeakerComponent>(uid);
            comp.SavedState.HadExaminer = EntityManager.HasComponent<ExaminerComponent>(uid);
            comp.SavedState.HadEmoting = HasComp<EmotingComponent>(uid);
            comp.SavedState.HadSpeechDead = false;
            if (EntityManager.TryGetComponent<SpeechComponent>(uid, out var speech) && speech != null)
            {
                comp.SavedState.HadSpeechDead = speech.SpeechVerb == "Dead";
                comp.SavedState.OldSpeechVerb = speech.SpeechVerb;
            }

            var visComp = EntityManager.EnsureComponent<VisibilityComponent>(uid);
            comp.SavedState.VisibilityLayers = visComp.Layer;
            if (EntityManager.TryGetComponent<EyeComponent>(uid, out var eyeGhost) && eyeGhost != null)
            {
                comp.SavedState.VisibilityMask = eyeGhost.VisibilityMask;
                comp.SavedState.DrawFov = eyeGhost.DrawFov;
            }

            if (EntityManager.TryGetComponent(uid, out PhysicsComponent? physics) && physics != null)
            {
                comp.SavedState.CanCollide = physics.CanCollide;
                comp.SavedState.BodyType = physics.BodyType;
                comp.SavedState.HadPhysics = true;
            }
            else
            {
                comp.SavedState.HadPhysics = false;
            }

            comp.SavedState.FixtureLayers = new List<int>();
            if (EntityManager.TryGetComponent(uid, out FixturesComponent? fixtures) && fixtures != null)
            {
                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    comp.SavedState.FixtureLayers.Add(fixture.CollisionLayer);
                }
            }

            comp.SavedState.HadContentEye = EntityManager.HasComponent<ContentEyeComponent>(uid);
            comp.SavedState.HadMovementIgnoreGravity = EntityManager.HasComponent<MovementIgnoreGravityComponent>(uid);
            comp.SavedState.HadCanMoveInAir = EntityManager.HasComponent<CanMoveInAirComponent>(uid);
        }
        private void SaveImmunities(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.SavedState == null)
                comp.SavedState = new GhostStateSnapshot();

            comp.SavedState.HadZombieImmune = EntityManager.HasComponent<ZombieImmuneComponent>(uid);
            comp.SavedState.HadBreathingImmunity = EntityManager.HasComponent<BreathingImmunityComponent>(uid);
            comp.SavedState.HadPressureImmunity = EntityManager.HasComponent<PressureImmunityComponent>(uid);
            comp.SavedState.HadActiveListener = EntityManager.HasComponent<ActiveListenerComponent>(uid);
            comp.SavedState.HadWeakToHoly = EntityManager.HasComponent<WeakToHolyComponent>(uid);
            comp.SavedState.HadCrematoriumImmune = EntityManager.HasComponent<CrematoriumImmuneComponent>(uid);
            comp.SavedState.HadGodmode = EntityManager.HasComponent<GodmodeComponent>(uid);
        }

        private void AddGhostComponents(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.SavedState == null) return;

            // Додаткові компоненти
            if (!comp.SavedState.HadFTLSmashImmune)
                EntityManager.EnsureComponent<FTLSmashImmuneComponent>(uid);
            if (!comp.SavedState.HadUniversalLanguageSpeaker)
                EntityManager.EnsureComponent<UniversalLanguageSpeakerComponent>(uid);
            if (!comp.SavedState.HadExaminer)
                EntityManager.EnsureComponent<ExaminerComponent>(uid);
            if (!comp.SavedState.HadEmoting)
                EnsureComp<EmotingComponent>(uid);
            if (!comp.SavedState.HadSpeechDead)
            {
                var speech = EntityManager.EnsureComponent<SpeechComponent>(uid);
                speech.SpeechVerb = "Dead";
            }

            EntityManager.EnsureComponent<MovementIgnoreGravityComponent>(uid);
            EntityManager.EnsureComponent<CanMoveInAirComponent>(uid);
            EntityManager.EnsureComponent<ContentEyeComponent>(uid);
            EntityManager.EnsureComponent<PhysicsComponent>(uid);
        }

        private void AddImmunities(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.SavedState == null) return;

            if (!comp.SavedState.HadZombieImmune)
                EntityManager.EnsureComponent<ZombieImmuneComponent>(uid);
            if (!comp.SavedState.HadBreathingImmunity)
                EntityManager.EnsureComponent<BreathingImmunityComponent>(uid);
            if (!comp.SavedState.HadPressureImmunity)
                EntityManager.EnsureComponent<PressureImmunityComponent>(uid);
            if (!comp.SavedState.HadActiveListener)
                EntityManager.EnsureComponent<ActiveListenerComponent>(uid);
            if (!comp.SavedState.HadWeakToHoly)
                EntityManager.EnsureComponent<WeakToHolyComponent>(uid).AlwaysTakeHoly = true;
            if (!comp.SavedState.HadCrematoriumImmune)
                EntityManager.EnsureComponent<CrematoriumImmuneComponent>(uid);
            if (!comp.SavedState.HadGodmode)
                EntityManager.EnsureComponent<GodmodeComponent>(uid);
        }

        private void SetupEyeForGhost(EntityUid uid, GhostTargetingComponent comp, bool ghostMode)
        {
            if (EntityManager.TryGetComponent<EyeComponent>(uid, out var eyeComp) && eyeComp != null)
            {
                var eyeSys = EntityManager.System<EyeSystem>();
                if (ghostMode)
                {
                    var mask = eyeComp.VisibilityMask | (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
                    mask |= (int) Content.Shared.Eye.VisibilityFlags.Ghost;
                    eyeSys.SetDrawFov(uid, false, eyeComp);
                    eyeSys.SetVisibilityMask(uid, mask, eyeComp);
                }
                else
                {
                    var mask = comp.SavedState?.VisibilityMask ?? eyeComp.VisibilityMask;
                    if (comp.SavedState?.DrawFov.HasValue == true)
                        eyeSys.SetDrawFov(uid, comp.SavedState.DrawFov.Value, eyeComp);
                    eyeSys.SetVisibilityMask(uid, mask, eyeComp);
                }
            }
        }

        private void SetupPhysicsForGhost(EntityUid uid, GhostTargetingComponent comp, bool ghostMode)
        {
            if (ghostMode)
            {
                if (EntityManager.TryGetComponent(uid, out PhysicsComponent? physics2) && physics2 != null)
                {
                    var physicsSys = EntityManager.System<SharedPhysicsSystem>();
                    physicsSys.SetCanCollide(uid, false, body: physics2);
                    physicsSys.SetBodyType(uid, Robust.Shared.Physics.BodyType.KinematicController, body: physics2);
                }
            }
            else
            {
                if (EntityManager.TryGetComponent(uid, out PhysicsComponent? physics) && physics != null)
                {
                    var physicsSys = EntityManager.System<SharedPhysicsSystem>();
                    if (comp.SavedState?.CanCollide.HasValue == true)
                        physicsSys.SetCanCollide(uid, comp.SavedState.CanCollide.Value, body: physics);
                    if (comp.SavedState?.BodyType.HasValue == true)
                        physicsSys.SetBodyType(uid, comp.SavedState.BodyType.Value, body: physics);
                }
            }
        }

        private void SetupVisibilityForGhost(EntityUid uid, GhostTargetingComponent comp, bool ghostMode)
        {
            var visibility = EntityManager.System<VisibilitySystem>();
            if (ghostMode)
                visibility.SetLayer(uid, (ushort) Content.Shared.Eye.VisibilityFlags.TargetingGhost);
            else if (comp.SavedState?.VisibilityLayers is { } original)
                visibility.SetLayer(uid, (ushort) original);
        }

        private void SetupEyePhysicsVisibility(EntityUid uid, GhostTargetingComponent comp, bool ghostMode)
        {
            SetupEyeForGhost(uid, comp, ghostMode);
            SetupPhysicsForGhost(uid, comp, ghostMode);
            SetupVisibilityForGhost(uid, comp, ghostMode);
        }

        private void UpdateActionIcon(EntityUid action, ToggleGhostFormActionEvent args, bool ghostMode)
        {
            if (EntityManager.TryGetComponent<ActionComponent>(action, out var actionComp))
            {
                if (ghostMode)
                {
                    EntityManager.System<SharedActionsSystem>()
                        .SetIcon(action,
                            new Robust.Shared.Utility.SpriteSpecifier.Texture(
                                new ResPath("Interface/Actions/eyeclose.png")));
                }
                else
                {
                    EntityManager.System<SharedActionsSystem>()
                        .SetIcon(action,
                            new Robust.Shared.Utility.SpriteSpecifier.Texture(
                                new ResPath("Interface/Actions/eyeopen.png")));
                }
            }
        }

        private void RemoveGhostComponents(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.SavedState == null) return;

            // Додаткові компоненти
            if (!comp.SavedState.HadFTLSmashImmune && EntityManager.HasComponent<FTLSmashImmuneComponent>(uid))
                EntityManager.RemoveComponent<FTLSmashImmuneComponent>(uid);
            if (!comp.SavedState.HadUniversalLanguageSpeaker && EntityManager.HasComponent<UniversalLanguageSpeakerComponent>(uid))
                EntityManager.RemoveComponent<UniversalLanguageSpeakerComponent>(uid);
            if (!comp.SavedState.HadExaminer && EntityManager.HasComponent<ExaminerComponent>(uid))
                EntityManager.RemoveComponent<ExaminerComponent>(uid);
            if (!comp.SavedState.HadEmoting)
                RemComp<EmotingComponent>(uid);
            if (!comp.SavedState.HadSpeechDead && EntityManager.HasComponent<SpeechComponent>(uid))
            {
                var speech = EntityManager.GetComponent<SpeechComponent>(uid);
                speech.SpeechVerb = comp.SavedState.OldSpeechVerb ?? "Normal";
            }

            // Fixtures: змінити layer на GhostImpassable
            if (EntityManager.TryGetComponent(uid, out FixturesComponent? fixtures) &&
                fixtures != null &&
                comp.SavedState.FixtureLayers != null)
            {
                var fixtureSys = EntityManager.System<FixtureSystem>();
                var fixturesList = fixtures.Fixtures.ToList();

                for (var i = 0; i < fixturesList.Count && i < comp.SavedState.FixtureLayers.Count; i++)
                {
                    var (id, fixture) = fixturesList[i];
                    var oldLayer = comp.SavedState.FixtureLayers[i];

                    if (fixture.CollisionLayer != oldLayer)
                    {
                        fixtureSys.DestroyFixture(uid, id, fixture, false);
                        fixtureSys.TryCreateFixture(
                            uid, fixture.Shape, id, fixture.Density, fixture.Hard,
                            oldLayer, fixture.CollisionMask, fixture.Friction,
                            fixture.Restitution, true
                        );
                    }
                }
            }

            if (!comp.SavedState.HadContentEye && EntityManager.HasComponent<ContentEyeComponent>(uid))
                EntityManager.RemoveComponent<ContentEyeComponent>(uid);
            if (!comp.SavedState.HadMovementIgnoreGravity && EntityManager.HasComponent<MovementIgnoreGravityComponent>(uid))
                EntityManager.RemoveComponent<MovementIgnoreGravityComponent>(uid);
            if (!comp.SavedState.HadCanMoveInAir && EntityManager.HasComponent<CanMoveInAirComponent>(uid))
                EntityManager.RemoveComponent<CanMoveInAirComponent>(uid);
            if (!comp.SavedState.HadPhysics && EntityManager.HasComponent<PhysicsComponent>(uid))
                EntityManager.RemoveComponent<PhysicsComponent>(uid);
        }

        private void RemoveImmunities(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.SavedState == null) return;

            if (!comp.SavedState.HadZombieImmune && EntityManager.HasComponent<ZombieImmuneComponent>(uid))
                EntityManager.RemoveComponent<ZombieImmuneComponent>(uid);
            if (!comp.SavedState.HadBreathingImmunity && EntityManager.HasComponent<BreathingImmunityComponent>(uid))
                EntityManager.RemoveComponent<BreathingImmunityComponent>(uid);
            if (!comp.SavedState.HadPressureImmunity && EntityManager.HasComponent<PressureImmunityComponent>(uid))
                EntityManager.RemoveComponent<PressureImmunityComponent>(uid);
            if (!comp.SavedState.HadActiveListener && EntityManager.HasComponent<ActiveListenerComponent>(uid))
                EntityManager.RemoveComponent<ActiveListenerComponent>(uid);
            if (!comp.SavedState.HadWeakToHoly && EntityManager.HasComponent<WeakToHolyComponent>(uid))
                EntityManager.RemoveComponent<WeakToHolyComponent>(uid);
            if (!comp.SavedState.HadCrematoriumImmune && EntityManager.HasComponent<CrematoriumImmuneComponent>(uid))
                EntityManager.RemoveComponent<CrematoriumImmuneComponent>(uid);
            if (!comp.SavedState.HadGodmode && EntityManager.HasComponent<GodmodeComponent>(uid))
                EntityManager.RemoveComponent<GodmodeComponent>(uid);
        }



        private void ClearSavedState(GhostTargetingComponent comp)
        {
            comp.SavedState = null;
        }
    }
}
