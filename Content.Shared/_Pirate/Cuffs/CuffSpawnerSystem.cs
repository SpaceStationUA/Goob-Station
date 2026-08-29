// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Coordinates;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Cuffs;

/// <summary>
/// Applies generated cuffs for security bots without polling for targets.
/// </summary>
public sealed class CuffSpawnerSystem : EntitySystem
{
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedCuffableSystem _cuff = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private EntityQuery<CuffableComponent> _cuffQuery;

    public override void Initialize()
    {
        base.Initialize();

        _cuffQuery = GetEntityQuery<CuffableComponent>();

        SubscribeLocalEvent<CuffSpawnerComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<CuffSpawnerComponent, CuffSpawnerDoAfterEvent>(OnCuff);
        SubscribeLocalEvent<CuffSpawnerComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<CuffSpawnerComponent, DoAfterAttemptEvent<CuffSpawnerDoAfterEvent>>(OnWait);
    }

    private void OnInteract(Entity<CuffSpawnerComponent> bot, ref UserActivateInWorldEvent args)
    {
        if (!CheckCuffs(bot.AsNullable(), args.Target))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            TimeSpan.FromSeconds(2),
            new CuffSpawnerDoAfterEvent(),
            args.User,
            args.Target)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
        });
    }

    private void OnCuff(Entity<CuffSpawnerComponent> bot, ref CuffSpawnerDoAfterEvent args)
    {
        if (!args.Cancelled && args.Target is { } target)
            TryCuff(bot.AsNullable(), target);
    }

    private void OnEmagged(Entity<CuffSpawnerComponent> bot, ref GotEmaggedEvent args)
    {
        args.Handled = true;
    }

    private void OnWait(Entity<CuffSpawnerComponent> bot, ref DoAfterAttemptEvent<CuffSpawnerDoAfterEvent> args)
    {
        if (args.Event.Target is not { } target || !CheckCuffs(bot.AsNullable(), target))
            args.Cancel();
    }

    public bool CheckCuffs(Entity<CuffSpawnerComponent?> bot, EntityUid target)
    {
        if (!Resolve(bot, ref bot.Comp, false))
            return false;

        if (!_cuffQuery.TryComp(target, out var cuffable) ||
            _cuff.IsCuffed((target, cuffable)))
        {
            return false;
        }

        return _hands.CountFreeHands(target) > 0;
    }

    public bool TryCuff(Entity<CuffSpawnerComponent?> bot, EntityUid target)
    {
        if (!Resolve(bot, ref bot.Comp, false) ||
            !CheckCuffs(bot, target) ||
            !_interaction.InRangeUnobstructed(bot.Owner, target))
        {
            return false;
        }

        var handcuffs = PredictedSpawnAtPosition(bot.Comp.HandcuffId, bot.Owner.ToCoordinates());
        if (_cuff.TryAddNewCuffs(target, bot.Owner, handcuffs))
            return true;

        PredictedQueueDel(handcuffs);
        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class CuffSpawnerDoAfterEvent : SimpleDoAfterEvent;
