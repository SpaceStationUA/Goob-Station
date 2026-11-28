// SPDX-FileCopyrightText: 2017-2026 Space Wizards Federation
// SPDX-FileCopyrightText: 2024-2026 Starlight
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;

namespace Content.Client._Pirate.Light;

public sealed class FireAndPuddleSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private FireAndPuddleOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FireAndPuddleOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<FireAndPuddleOverlay>();
        _overlay?.Dispose();
        _overlay = null;
        base.Shutdown();
    }
}
