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
using Content.Server.Chat.Systems;
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
            SubscribeLocalEvent<GhostTargetingComponent, CheckTargetedSpeechEvent>(OnTargetedSpeech);
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

            // EyeComponent: якщо вже є ціль, одразу оновити маску видимості
            var ghostLayer = (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
            if (ghost.Comp.Target != NetEntity.Invalid)
            {
                var targetUid = EntityManager.GetEntity(ghost.Comp.Target);
                if (EntityManager.TryGetComponent<EyeComponent>(targetUid, out var targetEye) && targetEye != null)
                {
                    var eyeSys = EntityManager.System<EyeSystem>();
                    eyeSys.SetVisibilityMask(targetUid, targetEye.VisibilityMask | ghostLayer, targetEye);
                }
            }
        }

        private void OnRemove(Entity<GhostTargetingComponent> ghost, ref ComponentRemove args)
        {
            if (ghost.Comp.ControlledTarget is not null)
                EndControl(ghost.Owner, ghost.Comp);

            // Відновлюємо стару форму, якщо entity був у ghost-формі
            if (ghost.Comp.SavedState?.VisibilityLayers.HasValue == true)
            {
                var uid = ghost.Owner;
                var visSys = EntityManager.System<VisibilitySystem>();

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

                // Відновити старі шари видимості
                if (EntityManager.TryGetComponent(uid, out VisibilityComponent? visComp2) && visComp2 != null)
                {
                    // Видаляємо TargetingGhost і Normal
                    visSys.RemoveLayer((uid, visComp2), (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost, false);
                    visSys.RemoveLayer((uid, visComp2), (int) Content.Shared.Eye.VisibilityFlags.Normal, false);

                    // Додаємо всі біти зі старого Layer
                    var layers = (ushort) ghost.Comp.SavedState.VisibilityLayers.Value;
                    for (var i = 0; i < 16; i++)
                    {
                        var flag = (ushort) (1 << i);
                        if ((layers & flag) != 0)
                            visSys.AddLayer((uid, visComp2), flag, false);
                    }

                    visSys.RefreshVisibility(uid, visibilityComponent: visComp2);
                }

                // Видалити компоненти, якщо їх не було до перетворення
                if (!ghost.Comp.SavedState.HadContentEye && EntityManager.HasComponent<ContentEyeComponent>(uid))
                    EntityManager.RemoveComponent<ContentEyeComponent>(uid);
                if (!ghost.Comp.SavedState.HadMovementIgnoreGravity &&
                    EntityManager.HasComponent<MovementIgnoreGravityComponent>(uid))
                    EntityManager.RemoveComponent<MovementIgnoreGravityComponent>(uid);
                if (!ghost.Comp.SavedState.HadCanMoveInAir && EntityManager.HasComponent<CanMoveInAirComponent>(uid))
                    EntityManager.RemoveComponent<CanMoveInAirComponent>(uid);
                if (!ghost.Comp.SavedState.HadPhysics && EntityManager.HasComponent<PhysicsComponent>(uid))
                    EntityManager.RemoveComponent<PhysicsComponent>(uid);
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
            var ghostLayer = (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
            var popupSys = EntityManager.System<SharedPopupSystem>();

            // Якщо була ціль, видаляємо маску з її ока
            if (comp.Target != NetEntity.Invalid)
            {
                var targetUid = EntityManager.GetEntity(comp.Target);
                if (EntityManager.TryGetComponent<EyeComponent>(targetUid, out var eye))
                {
                    var eyeSys = EntityManager.System<EyeSystem>();
                    eyeSys.SetVisibilityMask(targetUid, eye.VisibilityMask & ~ghostLayer, eye);
                }

                // Popup для користувача з ім'ям цілі
                var name = EntityManager.GetComponent<MetaDataComponent>(targetUid).EntityName;
                popupSys.PopupEntity($"Ціль '{name}' скинута!", uid, PopupType.Medium);
            }

            // Очищаємо ціль
            comp.Target = NetEntity.Invalid;
        }

        private void OnTargetedSpeech(EntityUid uid, GhostTargetingComponent comp, CheckTargetedSpeechEvent args)
        {
            if (!comp.IsGhost)
                return;

            // While in spirit form, suppress local speech entirely unless a valid target
            // was selected; the ChatSystem will deliver it only to that target.
            args.TargetsOnly = true;
            if (comp.Target == NetEntity.Invalid)
                return;

            var targetUid = EntityManager.GetEntity(comp.Target);
            if (EntityManager.EntityExists(targetUid))
                args.Targets.Add(targetUid);
        }

        public void OnSetTargetGhost(EntityUid uid, GhostTargetingComponent comp, SetTargetGhostActionEvent args)
        {
            var ghostLayer = (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
            var popupSys = EntityManager.System<SharedPopupSystem>();

            // Якщо була стара ціль, видаляємо маску з її ока
            if (comp.Target != NetEntity.Invalid)
            {
                var oldTargetUid = EntityManager.GetEntity(comp.Target);
                if (EntityManager.TryGetComponent<EyeComponent>(oldTargetUid, out var oldEye))
                {
                    var eyeSys = EntityManager.System<EyeSystem>();
                    eyeSys.SetVisibilityMask(oldTargetUid, oldEye.VisibilityMask & ~ghostLayer, oldEye);
                }

                // Додатковий popup про очищення старої цілі
                if (EntityManager.TryGetComponent<MetaDataComponent>(oldTargetUid, out var meta))
                {
                    var oldName = meta.EntityName;
                    popupSys.PopupEntity($"Стара ціль '{oldName}' очищена!", uid, PopupType.Small);
                }
            }

            // Встановлюємо нову ціль і даємо їй бачити привида
            if (args.Target != EntityUid.Invalid)
            {
                // Перевірка чи ціль не є самою entity
                if (args.Target == uid)
                {
                    popupSys.PopupEntity("Ви не можете встановити себе як ціль!", uid, PopupType.Medium);
                    return;
                }

                // Перевірка чи ціль існує
                if (!EntityManager.EntityExists(args.Target))
                {
                    popupSys.PopupEntity("Ціль не існує!", uid, PopupType.Medium);
                    return;
                }
                comp.Target = EntityManager.GetNetEntity(args.Target);

                // Додаємо маску новій цілі
                if (EntityManager.TryGetComponent(args.Target, out EyeComponent? newEye))
                {
                    var eyeSys = EntityManager.System<EyeSystem>();
                    eyeSys.SetVisibilityMask(args.Target, newEye.VisibilityMask | ghostLayer, newEye);
                }

                // Popup для користувача з ім'ям цілі
                var name = EntityManager.GetComponent<MetaDataComponent>(args.Target).EntityName;
                popupSys.PopupEntity($"Ціль встановлено: '{name}'!", uid, PopupType.Medium);
            }
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

                if (EntityManager.TryGetComponent<ActionComponent>(actionEntity, out var actionComp))
                {
                    EntityManager.System<SharedActionsSystem>()
                        .SetIcon(actionEntity,
                            new Robust.Shared.Utility.SpriteSpecifier.Texture(
                                new ResPath("Interface/Actions/eyeclose.png")));
                }

                popupSys.PopupEntity("Ви перетворилися на привида!", uid, PopupType.Medium);
            }
            else
            {
                RemoveGhostComponents(uid, comp);
                RemoveImmunities(uid, comp);
                SetupEyePhysicsVisibility(uid, comp, ghostMode: false);
                ClearSavedState(comp);
                comp.IsGhost = false;

                if (EntityManager.TryGetComponent<ActionComponent>(actionEntity, out var actionComp))
                {
                    EntityManager.System<SharedActionsSystem>()
                        .SetIcon(actionEntity,
                            new Robust.Shared.Utility.SpriteSpecifier.Texture(
                                new ResPath("Interface/Actions/eyeopen.png")));
                }

                popupSys.PopupEntity("Ви повернули свою стару форму!", uid, PopupType.Medium);
            }
        }

        private void OnTakeControlGhost(EntityUid uid, GhostTargetingComponent comp, TakeControlGhostActionEvent args)
        {
            var popupSys = EntityManager.System<SharedPopupSystem>();
            if (!comp.IsGhost || comp.ControlledTarget is not null)
                return;

            if (_timing.CurTime < comp.ControlCooldownEnd)
            {
                var remaining = Math.Ceiling((comp.ControlCooldownEnd - _timing.CurTime).TotalSeconds);
                popupSys.PopupEntity($"Здатність відновиться через {remaining:0} с.", uid, PopupType.Medium);
                return;
            }

            if (comp.Target == NetEntity.Invalid)
            {
                popupSys.PopupEntity("Спершу оберіть тіло для контролю.", uid, PopupType.Medium);
                return;
            }

            var target = EntityManager.GetEntity(comp.Target);
            if (!EntityManager.EntityExists(target) || target == uid ||
                !TryComp<MobStateComponent>(target, out var targetState) ||
                targetState.CurrentState == MobState.Dead)
            {
                popupSys.PopupEntity("Обрана ціль не є живим тілом.", uid, PopupType.Medium);
                return;
            }

            if (!_mind.TryGetMind(uid, out var ghostMind, out _))
            {
                popupSys.PopupEntity("Для контролю потрібна свідомість у вашому тілі.", uid, PopupType.Medium);
                return;
            }

            var hasOriginalMind = _mind.TryGetMind(target, out var targetMind, out var targetMindComp);
            if (!hasOriginalMind && HasComp<ActorComponent>(target))
            {
                popupSys.PopupEntity("Не вдалося безпечно відокремити свідомість цілі.", uid, PopupType.Medium);
                return;
            }

            comp.ControlContainer ??= _containers.EnsureContainer<Container>(uid, "GhostControlContainer");
            var mindHolder = Spawn(prototype: null, coordinates: MapCoordinates.Nullspace);
            EnsureComp<MindContainerComponent>(mindHolder);

            if (!_containers.Insert(mindHolder, comp.ControlContainer))
            {
                Del(mindHolder);
                popupSys.PopupEntity("Не вдалося підготувати тіло для контролю.", uid, PopupType.Medium);
                return;
            }

            comp.ControlledTarget = target;
            comp.ControlledMind = ghostMind;
            comp.OriginalTargetMind = hasOriginalMind ? targetMind : null;
            comp.ControlMindHolder = mindHolder;

            if (hasOriginalMind)
                _mind.TransferTo(targetMind, mindHolder, mind: targetMindComp);

            _mind.TransferTo(ghostMind, target);
            comp.ControlTimeEnd = _timing.CurTime + comp.ControlDuration;
            comp.ControlCooldownEnd = _timing.CurTime + comp.ControlCooldown;

            if (TryComp<GhostRoleComponent>(uid, out var ghostRole))
                _ghostRoles.UnregisterGhostRole((uid, ghostRole));

            popupSys.PopupEntity("Ви тимчасово заволоділи тілом цілі.", target, target, PopupType.Medium);
        }

        private void EndControl(EntityUid uid, GhostTargetingComponent comp)
        {
            if (comp.ControlledTarget is not { } target)
                return;

            var sourceExists = !TerminatingOrDeleted(uid);
            var targetExists = !TerminatingOrDeleted(target);

            if (comp.ControlledMind is { } controlledMind && !TerminatingOrDeleted(controlledMind))
            {
                EntityUid? returnTo = sourceExists ? uid : null;
                _mind.TransferTo(controlledMind, returnTo);
            }

            if (comp.OriginalTargetMind is { } originalMind && !TerminatingOrDeleted(originalMind))
                _mind.TransferTo(originalMind, targetExists ? target : null);

            if (sourceExists && TryComp<GhostRoleComponent>(uid, out var ghostRole))
                _ghostRoles.RegisterGhostRole((uid, ghostRole));

            if (comp.ControlContainer != null)
                _containers.CleanContainer(comp.ControlContainer);

            comp.ControlledTarget = null;
            comp.ControlledMind = null;
            comp.OriginalTargetMind = null;
            comp.ControlMindHolder = null;
            comp.ControlTimeEnd = TimeSpan.Zero;

            if (sourceExists)
                EntityManager.System<SharedPopupSystem>().PopupEntity("Контроль над тілом завершився.", uid, PopupType.Medium);
        }

        //  Приватні методи для логіки
        private void SaveState(EntityUid uid, GhostTargetingComponent comp)
        {
            comp.SavedState = new GhostStateSnapshot();

            // Додаткові компоненти
            comp.SavedState.HadFTLSmashImmune = EntityManager.HasComponent<FTLSmashImmuneComponent>(uid);
            comp.SavedState.HadUniversalLanguageSpeaker = EntityManager.HasComponent<UniversalLanguageSpeakerComponent>(uid);
            comp.SavedState.HadExaminer = EntityManager.HasComponent<ExaminerComponent>(uid);
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
                    var mask = eyeComp.VisibilityMask | (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost;
                    mask &= ~(int) Content.Shared.Eye.VisibilityFlags.Ghost;
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
            var visSys = EntityManager.System<VisibilitySystem>();
            if (ghostMode)
            {
                var visComp2 = EntityManager.EnsureComponent<VisibilityComponent>(uid);
                visSys.RemoveLayer((uid, visComp2), (int) Content.Shared.Eye.VisibilityFlags.Normal, false);
                visSys.AddLayer((uid, visComp2), (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost, false);
                visSys.RefreshVisibility(uid, visibilityComponent: visComp2);
            }
            else
            {
                if (comp.SavedState?.VisibilityLayers.HasValue == true)
                {
                    var visComp2 = EntityManager.EnsureComponent<VisibilityComponent>(uid);
                    visSys.RemoveLayer((uid, visComp2), (int) Content.Shared.Eye.VisibilityFlags.TargetingGhost, false);
                    visSys.RemoveLayer((uid, visComp2), (int) Content.Shared.Eye.VisibilityFlags.Normal, false);
                    var layers = (ushort) comp.SavedState.VisibilityLayers.Value;
                    for (var i = 0; i < 16; i++)
                    {
                        var flag = (ushort) (1 << i);
                        if ((layers & flag) != 0)
                            visSys.AddLayer((uid, visComp2), flag, false);
                    }

                    visSys.RefreshVisibility(uid, visibilityComponent: visComp2);
                }
            }
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
