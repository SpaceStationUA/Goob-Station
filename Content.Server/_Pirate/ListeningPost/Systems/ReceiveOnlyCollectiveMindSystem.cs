// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Components;
using Content.Shared._Starlight.CollectiveMind;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class ReceiveOnlyCollectiveMindSystem : EntitySystem
{
    [Dependency] private readonly CollectiveMindUpdateSystem _collectiveMind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReceiveOnlyCollectiveMindComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<ReceiveOnlyCollectiveMindComponent> ent, ref ComponentStartup args)
    {
        var collectiveMind = EnsureComp<CollectiveMindComponent>(ent);
        collectiveMind.Channels.Add(ent.Comp.Channel);
        _collectiveMind.UpdateCollectiveMind(ent, collectiveMind);
        Dirty(ent, collectiveMind);
    }
}
