// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.DeviceLinking.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Hands;
using Content.Shared.Item.ItemToggle;

namespace Content.Server._Pirate.DeviceLinking.Systems;

public sealed class DeadMansSignallerSystem : EntitySystem
{
    [Dependency] private readonly DeviceLinkSystem _link = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeadMansSignallerComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnUnequipped(Entity<DeadMansSignallerComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (_toggle.IsActivated(ent.Owner))
            _link.InvokePort(ent.Owner, ent.Comp.Port);
    }
}
