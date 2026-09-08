// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Shared._Pirate.ListeningPost.DropConsole;
using Content.Shared.Power;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class SyndicateDropPadSystem : EntitySystem
{
    private static readonly TimeSpan SendVisualDuration = TimeSpan.FromSeconds(1);

    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _sending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyndicateDropPadComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SyndicateDropPadComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnMapInit(Entity<SyndicateDropPadComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnPowerChanged(Entity<SyndicateDropPadComponent> ent, ref PowerChangedEvent args)
    {
        UpdateVisuals(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_sending.Count == 0)
            return;

        var now = _timing.CurTime;
        var finished = new ValueList<EntityUid>();

        foreach (var (uid, until) in _sending)
        {
            if (now >= until)
                finished.Add(uid);
        }

        foreach (var uid in finished)
        {
            _sending.Remove(uid);

            if (TryComp<SyndicateDropPadComponent>(uid, out var pad))
                UpdateVisuals((uid, pad));
        }
    }

    public bool IsOperational(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
    }

    public EntityUid? GetPayload(Entity<SyndicateDropPadComponent> ent)
    {
        var found = _lookup.GetEntitiesInRange(Transform(ent).Coordinates,
            ent.Comp.PayloadRange,
            LookupFlags.Uncontained);

        foreach (var candidate in found)
        {
            if (candidate == ent.Owner)
                continue;

            if (_whitelist.IsWhitelistFailOrNull(ent.Comp.Whitelist, candidate))
                continue;

            return candidate;
        }

        return null;
    }

    public void PlayError(Entity<SyndicateDropPadComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.ErrorSound, ent);
    }

    public void PlaySend(Entity<SyndicateDropPadComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.SendSound, ent);

        _sending[ent.Owner] = _timing.CurTime + SendVisualDuration;
        _appearance.SetData(ent, SyndicateDropPadVisuals.State, SyndicateDropPadState.Sending);
    }

    private void UpdateVisuals(Entity<SyndicateDropPadComponent> ent)
    {
        if (_sending.ContainsKey(ent.Owner))
            return;

        var state = IsOperational(ent) ? SyndicateDropPadState.Idle : SyndicateDropPadState.Unpowered;
        _appearance.SetData(ent, SyndicateDropPadVisuals.State, state);
    }
}
