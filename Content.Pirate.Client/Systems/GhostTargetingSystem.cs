// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chat.Managers;
using Content.Pirate.Shared;
using Content.Pirate.Shared.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Pirate.Client.Systems;

public sealed class GhostTargetingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<TargetedGhostEmoteMessage>(OnEmote);
        SubscribeLocalEvent<GhostTargetingComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<GhostTargetingComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnEmote(TargetedGhostEmoteMessage message)
    {
        if (TryGetEntity(message.Spirit, out var spirit) && _prototypes.TryIndex(message.Emote, out var emote) &&
            emote.Event != null)
            RaiseLocalEvent(spirit.Value, emote.Event);
    }

    private void OnState(Entity<GhostTargetingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_players.LocalEntity == ent.Owner)
            _chat.UpdatePermissions();
    }

    private void OnShutdown(Entity<GhostTargetingComponent> ent, ref ComponentShutdown args)
    {
        if (_players.LocalEntity == ent.Owner)
            _chat.UpdatePermissions();
    }
}
