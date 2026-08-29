// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Clothing.WeldingVisor;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Pirate.Clothing.WeldingVisor;

/// <summary>Pirate: welding visor - manages the local visor overlay.</summary>
public sealed class WeldingVisorOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private WeldingVisorOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeldingVisorImpairedComponent, ComponentInit>(OnImpairedInit);
        SubscribeLocalEvent<WeldingVisorImpairedComponent, ComponentShutdown>(OnImpairedShutdown);

        SubscribeLocalEvent<WeldingVisorImpairedComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<WeldingVisorImpairedComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnPlayerAttached(EntityUid uid, WeldingVisorImpairedComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, WeldingVisorImpairedComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnImpairedInit(EntityUid uid, WeldingVisorImpairedComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnImpairedShutdown(EntityUid uid, WeldingVisorImpairedComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.RemoveOverlay(_overlay);
    }
}
