// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.TerrorSpider;
using Robust.Shared.Player;

namespace Content.Pirate.Client.TerrorSpider;

public sealed class TerrorSpiderWebOccluderSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;

    private readonly HashSet<EntityUid> _locallyDisabled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderWebOccluderComponent, ComponentShutdown>(OnWebOccluderShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        var shouldIgnoreWebOccluders = _player.LocalEntity is { Valid: true } player
            && HasComp<TerrorSpiderComponent>(player);

        if (shouldIgnoreWebOccluders)
        {
            DisableWebOccluders();
            return;
        }

        RestoreWebOccluders();
    }

    private void OnWebOccluderShutdown(Entity<TerrorSpiderWebOccluderComponent> ent, ref ComponentShutdown args)
    {
        _locallyDisabled.Remove(ent.Owner);
    }

    private void DisableWebOccluders()
    {
        var query = EntityQueryEnumerator<TerrorSpiderWebOccluderComponent, OccluderComponent>();
        while (query.MoveNext(out var uid, out _, out var occluder))
        {
            _locallyDisabled.Add(uid);

            if (occluder.Enabled)
                _occluder.SetEnabled(uid, false, occluder);
        }
    }

    private void RestoreWebOccluders()
    {
        if (_locallyDisabled.Count == 0)
            return;

        var toRestore = new EntityUid[_locallyDisabled.Count];
        var count = 0;

        foreach (var uid in _locallyDisabled)
        {
            toRestore[count] = uid;
            count++;
        }

        _locallyDisabled.Clear();

        for (var i = 0; i < count; i++)
        {
            var uid = toRestore[i];

            if (TryComp<OccluderComponent>(uid, out var occluder) && !occluder.Enabled)
                _occluder.SetEnabled(uid, true, occluder);
        }
    }
}
