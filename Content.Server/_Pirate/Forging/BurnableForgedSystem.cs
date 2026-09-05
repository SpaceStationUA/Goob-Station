// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Forging;
using Content.Shared.Audio;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Forging;

/// <summary>
/// Burns an overheated forged entity in direct response to a temperature event.
/// The zero-delay callback deduplicates repeated temperature notifications without polling.
/// </summary>
public sealed class BurnableForgedSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BurnableForgedComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
        SubscribeLocalEvent<BurnableForgedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnTemperatureChanged(Entity<BurnableForgedComponent> ent, ref OnTemperatureChangeEvent args)
    {
        if (args.CurrentTemperature < ent.Comp.BurnTemp || !_pending.Add(ent.Owner))
            return;

        Timer.Spawn(0, () => CompleteBurn(ent.Owner));
    }

    private void OnShutdown(Entity<BurnableForgedComponent> ent, ref ComponentShutdown args)
    {
        _pending.Remove(ent.Owner);
    }

    public bool CompleteBurn(EntityUid uid)
    {
        _pending.Remove(uid);
        if (!TryComp<BurnableForgedComponent>(uid, out var burnable) ||
            !TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(uid, out var temperature) ||
            temperature.CurrentTemperature < burnable.BurnTemp)
        {
            return false;
        }

        var transform = Transform(uid);
        var result = Spawn(burnable.BurnedPrototype, transform.Coordinates);
        _metaData.SetEntityName(result, Loc.GetString(burnable.BurnedPrefix, ("name", Name(uid))));
        _popup.PopupEntity(Loc.GetString(burnable.BurnedPopup, ("name", uid)), uid);
        _audio.PlayPvs(burnable.BurnSound, uid);
        QueueDel(uid);
        return true;
    }
}
