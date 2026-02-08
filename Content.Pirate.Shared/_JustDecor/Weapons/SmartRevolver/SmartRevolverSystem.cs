using Content.Pirate.Shared._JustDecor.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Verbs;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared.Damage;

namespace Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver;

/// <summary>
/// System that handles smart revolver target selection, cycling, and ricochet bullet creation.
/// </summary>
public sealed class SmartRevolverSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionContainerSystem _actions = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly Content.Shared.Hands.EntitySystems.SharedHandsSystem _hands = default!;
    [Dependency] private readonly Robust.Shared.Random.IRobustRandom _random = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartRevolverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SmartRevolverComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<SmartRevolverComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SmartRevolverComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<SmartRevolverComponent, CycleSmartRevolverTargetEvent>(OnCycleTarget);
        SubscribeLocalEvent<SmartRevolverComponent, GetItemActionsEvent>(OnGetItemActions);

        SubscribeNetworkEvent<SmartRevolverSetTargetMessage>(OnSetTargetMessage);
    }

    private void OnMapInit(EntityUid uid, SmartRevolverComponent component, MapInitEvent args)
    {
        _actions.EnsureAction(uid, ref component.CycleTargetAction, "ActionCycleSmartRevolverTarget");
    }

    private void OnGetItemActions(EntityUid uid, SmartRevolverComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.CycleTargetAction, "ActionCycleSmartRevolverTarget");
    }

    private void OnAlternativeVerb(EntityUid uid, SmartRevolverComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !IsValidTarget(args.Target))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => SetTarget(uid, component, args.Target, args.User),
            Text = Loc.GetString("smart-revolver-target-selection"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/scope.svg.192dpi.png")),
            Priority = 100
        });
    }

    private void OnSetTargetMessage(SmartRevolverSetTargetMessage msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;
        if (user == null || !_combatMode.IsInCombatMode(user.Value))
            return;

        if (!_hands.TryGetActiveItem(user.Value, out var heldEntity))
            return;

        if (!TryComp<SmartRevolverComponent>(heldEntity, out var component))
            return;

        var target = GetEntity(msg.Target);

        // Логіка: якщо клікнути на ту саму ціль, очистити
        if (component.SelectedTarget == target)
        {
            ClearTarget(heldEntity.Value, component, user.Value);
            return;
        }
        if (msg.Target == NetEntity.Invalid || !IsValidTarget(target))
        {
            ClearTarget(heldEntity.Value, component, user.Value);
            return;
        }

        SetTarget(heldEntity.Value, component, target, user.Value);
    }

    private void OnAfterInteract(EntityUid uid, SmartRevolverComponent component, AfterInteractEvent args)
    {

    }

    private void OnAmmoShot(EntityUid uid, SmartRevolverComponent component, ref AmmoShotEvent args)
    {
        if (!TryComp(uid, out GunComponent? gun))
            return;

        var target = component.SelectedTarget ?? gun.Target;

        if (target == null)
            return;

        // Очищення, якщо ціль втрачається
        if (!Exists(target.Value) || Deleted(target.Value))
        {
            ClearTarget(uid, component, null);
            return;
        }

        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<ProjectileComponent>(projectile, out var proj))
            {
                proj.DeleteOnCollide = false;
                Dirty(projectile, proj);
            }

            var ricochet = EnsureComp<RicochetProjectileComponent>(projectile);
            ricochet.Target = target;

            ricochet.TargetBounces = _random.Next(component.MinRicochets, component.MaxRicochets + 1);
            ricochet.MaxBounces = ricochet.TargetBounces;

            ricochet.FollowPlannedPath = true;
            Dirty(projectile, ricochet);
        }
    }

    private void OnCycleTarget(EntityUid uid, SmartRevolverComponent component, CycleSmartRevolverTargetEvent args)
    {
        if (args.Handled)
            return;

        if (!args.Performer.IsValid())
            return;

        UpdateAvailableTargets(uid, component, args.Performer);

        if (component.AvailableTargets.Count == 0)
        {
            _popup.PopupEntity("No valid targets in view!", uid, PopupType.Medium);
            args.Handled = true;
            return;
        }

        component.CurrentTargetIndex = (component.CurrentTargetIndex + 1) % component.AvailableTargets.Count;
        var newTarget = component.AvailableTargets[component.CurrentTargetIndex];

        SetTarget(uid, component, newTarget, null);
        args.Handled = true;
    }

    public void SetTarget(EntityUid revolverUid, SmartRevolverComponent component, EntityUid target, EntityUid? user)
    {
        component.SelectedTarget = target;
        Dirty(revolverUid, component);

        if (_net.IsServer)
        {
            var targetName = MetaData(target).EntityName;
            var message = $"Ціль встановлена: {targetName}";

            if (user != null && Exists(user.Value))
            {
                _popup.PopupEntity(message, revolverUid, user.Value, PopupType.Medium);
            }
            else
            {
                _popup.PopupEntity(message, revolverUid, PopupType.Medium);
            }
        }
    }

    public void ClearTarget(EntityUid revolverUid, SmartRevolverComponent component, EntityUid? user)
    {
        if (component.SelectedTarget == null)
            return;

        component.SelectedTarget = null;
        component.AvailableTargets.Clear();
        component.CurrentTargetIndex = 0;
        Dirty(revolverUid, component);

        if (_net.IsServer)
        {
            var message = "Ціль очищена";

            if (user != null && Exists(user.Value))
            {
                _popup.PopupEntity(message, revolverUid, user.Value, PopupType.Small);
            }
            else
            {
                _popup.PopupEntity(message, revolverUid, PopupType.Small);
            }
        }
    }

    private void UpdateAvailableTargets(EntityUid revolverUid, SmartRevolverComponent component, EntityUid user)
    {
        component.AvailableTargets.Clear();

        var revolverPos = _transform.GetMapCoordinates(revolverUid);
        var query = EntityQueryEnumerator<TransformComponent>();

        while (query.MoveNext(out var uid, out var xform))
        {
            if (uid == revolverUid || uid == user)
                continue;

            if (!IsValidCycleTarget(uid))
                continue;

            if (!_examine.InRangeUnOccluded(user, uid, component.MaxTargetDistance))
                continue;

            var targetPos = _transform.GetMapCoordinates(uid, xform);
            if (targetPos.MapId != revolverPos.MapId)
                continue;

            var distance = (targetPos.Position - revolverPos.Position).Length();
            if (distance > component.MaxTargetDistance)
                continue;

            component.AvailableTargets.Add(uid);
        }
    }

    private bool IsValidTarget(EntityUid target)
    {
        // Перевіряємо чи ціль існує
        if (!Exists(target) || Deleted(target))
            return false;

        // Перевіряємо наявність MobStateComponent або DamageableComponent
        return HasComp<MobStateComponent>(target) ||
               HasComp<Content.Shared.Damage.DamageableComponent>(target);
    }

    private bool IsValidCycleTarget(EntityUid target)
    {
        if (!Exists(target) || Deleted(target))
            return false;

        return HasComp<MobStateComponent>(target) && HasComp<DamageableComponent>(target);
    }
}
