// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Changeling.Components;
using Content.Server.Body.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Changeling;

public sealed class ChangelingEggSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ChangelingSystem _changeling = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ChangelingEggComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.UpdateTimer)
                continue;

            comp.UpdateTimer = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdateCooldown);

            Cycle(uid, comp);
        }
    }

    public void Cycle(EntityUid uid, ChangelingEggComponent comp)
    {
        if (!TryComp<ChangelingEggComponent>(uid, out var current) || current != comp)
            return;

        if (!comp.Active)
        {
            comp.Active = true;
            return;
        }

        if (TerminatingOrDeleted(comp.LingMind))
        {
            _bodySystem.GibBody(uid);
            return;
        }

        var newUid = Spawn("MobMonkey", Transform(uid).Coordinates);

        // Pirate: initialize the complete body before attaching a connected or reconnecting player.
        foreach (var component in comp.LingComponents)
            _changeling.RestoreLastResortComponent(newUid, component);

        EnsureComp<MindContainerComponent>(newUid);
        _mind.TransferTo(comp.LingMind, newUid);

        comp.LingComponents.Clear();
        RemComp<ChangelingEggComponent>(uid);
        _bodySystem.GibBody(uid);
    }
}
