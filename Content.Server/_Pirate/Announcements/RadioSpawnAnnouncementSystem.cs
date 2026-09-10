// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Chat;

namespace Content.Server._Pirate.Announcements;

/// <summary>Announces ghost-role spawns over radio.</summary>
public sealed class RadioSpawnAnnouncementSystem : EntitySystem
{
    [Dependency] private readonly RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The event is raised on the spawned mob, not the spawner.
        SubscribeLocalEvent<GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        SubscribeLocalEvent<RadioSpawnAnnouncementComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
    }

    private void OnSpawnerUsed(GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<RadioSpawnAnnouncementComponent>(args.Spawner, out var comp))
            return;

        var job = comp.Job is { } job1
            ? Loc.GetString(job1)
            : CompOrNull<GhostRoleComponent>(args.Spawner)?.RoleName ?? string.Empty;

        var message = Loc.GetString(comp.Message, ("character", args.Spawned), ("job", job));

        _radio.SendRadioMessage(args.Spawner, message, comp.Channel, args.Spawner);
    }

    private void OnTransformSpeakerName(Entity<RadioSpawnAnnouncementComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (ent.Comp.SenderName is { } sender)
            args.VoiceName = Loc.GetString(sender);
    }
}
