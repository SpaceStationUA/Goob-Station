// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Suspicion;

public sealed class SuspicionAllyMarkerSystem : EntitySystem
{
    private static readonly ProtoId<FactionIconPrototype> TraitorIcon = "SyndicateFaction";

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private NetEntity _viewer;
    private NetEntity[] _traitors = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SuspicionAllyRosterUpdated>(OnRosterUpdated);
        SubscribeNetworkEvent<SuspicionHudClosed>(_ => Clear());
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SuspicionPlayerComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        _players.LocalSessionChanged += OnSessionChanged;
    }

    public override void Shutdown()
    {
        _players.LocalSessionChanged -= OnSessionChanged;
        base.Shutdown();
    }

    private void OnRosterUpdated(SuspicionAllyRosterUpdated args)
    {
        _viewer = args.Viewer;
        _traitors = args.Traitors;
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_traitors.Length != 0 && TryComp<MetaDataComponent>(args.Entity, out var metadata) &&
            GetNetEntity(args.Entity, metadata) == _viewer)
            Clear();
    }

    private void OnSessionChanged((ICommonSession? Old, ICommonSession? New) sessions) => Clear();

    private void Clear()
    {
        _viewer = NetEntity.Invalid;
        _traitors = [];
    }

    private void OnGetStatusIcons(EntityUid uid, SuspicionPlayerComponent component, ref GetStatusIconsEvent args)
    {
        if (_traitors.Length == 0 || _players.LocalEntity is not {} viewer || GetNetEntity(viewer) != _viewer ||
            Array.IndexOf(_traitors, GetNetEntity(uid)) < 0)
            return;

        args.StatusIcons.Add(_prototypes.Index(TraitorIcon));
    }
}
