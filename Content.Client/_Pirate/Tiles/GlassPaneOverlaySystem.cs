// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Tiles;

public sealed class GlassPaneOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new GlassPaneOverlay(EntityManager, _proto, _resources, _tileDefs));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<GlassPaneOverlay>();
    }
}
