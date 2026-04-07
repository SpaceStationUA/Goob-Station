using System;
using Content.Pirate.Shared._JustDecor.Missions.Components;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Server.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Maths;

namespace Content.Pirate.Server._JustDecor.Missions.Systems;

public sealed class MissionSelfDestructSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MissionSelfDestructComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(Entity<MissionSelfDestructComponent> ent, ref UseInHandEvent args)
    {
        if (ent.Comp.Activated)
        {
            _popup.PopupEntity("Самознищення вже активоване.", args.User, args.User);
            return;
        }

        ent.Comp.Activated = true;
        ent.Comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Countdown);
        ent.Comp.NextAnnouncementIndex = 0;
        while (ent.Comp.NextAnnouncementIndex < ent.Comp.AnnounceAtSeconds.Count
               && ent.Comp.AnnounceAtSeconds[ent.Comp.NextAnnouncementIndex] >= ent.Comp.Countdown)
        {
            ent.Comp.NextAnnouncementIndex++;
        }

        Announce(ent, (int) MathF.Round(ent.Comp.Countdown));
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MissionSelfDestructComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Activated || comp.EndTime == null)
                continue;

            var remaining = comp.EndTime.Value - _timing.CurTime;
            if (remaining <= TimeSpan.Zero)
            {
                var mapCoords = _transform.ToMapCoordinates(xform.Coordinates);
                _explosion.QueueExplosion(
                    mapCoords,
                    comp.ExplosionPrototype,
                    comp.ExplosionTotalIntensity,
                    comp.ExplosionSlope,
                    comp.ExplosionMaxTileIntensity,
                    uid);

                QueueDel(uid);
                continue;
            }

            while (comp.NextAnnouncementIndex < comp.AnnounceAtSeconds.Count)
            {
                var next = comp.AnnounceAtSeconds[comp.NextAnnouncementIndex];
                if (remaining.TotalSeconds > next)
                    break;

                Announce((uid, comp), next);
                comp.NextAnnouncementIndex++;
            }
        }
    }

    private void Announce(Entity<MissionSelfDestructComponent> ent, int secondsLeft)
    {
        var msg = secondsLeft >= 60
            ? $"Самознищення активовано. Залишилось {secondsLeft / 60} хв."
            : $"Самознищення активовано. Залишилось {secondsLeft} с.";

        _chat.DispatchGlobalAnnouncement(msg, ent.Comp.AnnouncementSender, colorOverride: Color.Red);
    }
}
