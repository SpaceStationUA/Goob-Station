// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Research.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Genetics.Mutations;

[RegisterComponent, Access(typeof(MutationSystem))]
public sealed partial class GeneticsDiscoveryComponent : Component
{
    [DataField(serverOnly: true)]
    public HashSet<EntProtoId<MutationComponent>> Discovered = new();
}

public sealed partial class MutationSystem
{
    public EntityUid? GetDiscoveryServer(EntityUid device)
        => CompOrNull<ResearchClientComponent>(device)?.Server;

    public bool IsDiscovered(EntityUid device, EntProtoId<MutationComponent> id)
        => GetDiscoveryServer(device) is {} server &&
            CompOrNull<GeneticsDiscoveryComponent>(server)?.Discovered.Contains(id) == true;

    public bool Discover(EntityUid device, EntProtoId<MutationComponent> id)
    {
        if (_net.IsClient || GetDiscoveryServer(device) is not {} server)
            return false;

        EnsureComp<GeneticsDiscoveryComponent>(server).Discovered.Add(id);
        return true;
    }
}
