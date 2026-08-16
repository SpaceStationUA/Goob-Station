// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Audio;
using Content.Server.Lathe.Components;
using Content.Shared._Pirate.Audio;

namespace Content.Server._Pirate.Lathe;

public sealed class LatheSoundLoopSystem : EntitySystem
{
    [Dependency] private readonly SequencedSoundLoopSystem _soundLoop = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheProducingComponent, ComponentStartup>(OnProducingStartup);
        SubscribeLocalEvent<LatheProducingComponent, ComponentShutdown>(OnProducingShutdown);
    }

    private void OnProducingStartup(Entity<LatheProducingComponent> ent, ref ComponentStartup args)
    {
        _soundLoop.StartLoop(ent.Owner);
    }

    private void OnProducingShutdown(Entity<LatheProducingComponent> ent, ref ComponentShutdown args)
    {
        _soundLoop.StopLoop(ent.Owner);
    }
}
