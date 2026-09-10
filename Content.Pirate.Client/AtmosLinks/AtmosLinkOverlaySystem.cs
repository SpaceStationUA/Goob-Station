// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.AtmosLinks;
using Robust.Client.Graphics;

namespace Content.Pirate.Client.AtmosLinks;

public sealed class AtmosLinkOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public List<AtmosLinkGroup> Groups = new();
    public List<AtmosLinkOrphan> Orphans = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AtmosLinkOverlayDataEvent>(OnData);
        SubscribeNetworkEvent<AtmosLinkOverlayDisableEvent>(OnDisable);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Clear();
    }

    private void OnData(AtmosLinkOverlayDataEvent ev)
    {
        Groups = ev.Groups;
        Orphans = ev.Orphans;

        if (!_overlay.HasOverlay<AtmosLinkOverlay>())
            _overlay.AddOverlay(new AtmosLinkOverlay(this));
    }

    private void OnDisable(AtmosLinkOverlayDisableEvent ev)
    {
        Clear();
    }

    private void Clear()
    {
        Groups = new List<AtmosLinkGroup>();
        Orphans = new List<AtmosLinkOrphan>();
        _overlay.RemoveOverlay<AtmosLinkOverlay>();
    }
}
