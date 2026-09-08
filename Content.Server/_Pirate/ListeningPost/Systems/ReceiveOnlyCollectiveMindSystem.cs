// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.ListeningPost.Interception;
using Content.Shared._Starlight.CollectiveMind;

namespace Content.Server._Pirate.ListeningPost.Systems;

/// <inheritdoc cref="ReceiveOnlyCollectiveMindComponent"/>
public sealed class ReceiveOnlyCollectiveMindSystem : EntitySystem
{
    [Dependency] private readonly CollectiveMindUpdateSystem _collectiveMind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReceiveOnlyCollectiveMindComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ReceiveOnlyCollectiveMindComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<ReceiveOnlyCollectiveMindComponent> ent, ref ComponentStartup args)
    {
        // Every mob carries an empty CollectiveMind from BaseMob, so this is an EnsureComp for the rest.
        var collectiveMind = EnsureComp<CollectiveMindComponent>(ent);
        ent.Comp.GrantedChannel = collectiveMind.Channels.Add(ent.Comp.Channel);

        Sync(ent, collectiveMind);
    }

    private void OnShutdown(Entity<ReceiveOnlyCollectiveMindComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent)
            || !ent.Comp.GrantedChannel
            || !TryComp<CollectiveMindComponent>(ent, out var collectiveMind))
            return;

        collectiveMind.Channels.Remove(ent.Comp.Channel);
        Sync(ent, collectiveMind);
    }

    private void Sync(EntityUid uid, CollectiveMindComponent collectiveMind)
    {
        // Channels is only the intent; Minds is what chat actually routes on.
        _collectiveMind.UpdateCollectiveMind(uid, collectiveMind);
        Dirty(uid, collectiveMind);
    }
}
