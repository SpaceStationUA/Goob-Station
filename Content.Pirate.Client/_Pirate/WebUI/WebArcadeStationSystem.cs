// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Gives arcade machines the Pirate WebArcade verb ("Игровой автомат"):
///     right-click a machine in world and open the CEF game in a window.
///     Entities whose prototype inherits <c>ArcadeBase</c> get a <see cref="WebArcadeComponent"/>
///     client-side when they are created, so the server layer stays untouched.
/// </summary>
public sealed partial class WebArcadeStationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private const string ArcadeBaseId = "ArcadeBase";

    // Matched by inheritance, not by "Arcade" in the ID: that also caught signs, floor tiles,
    // circuitboards and the arcade holopad.
    private readonly HashSet<string> _arcadePrototypes = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WebArcadeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        // Replaces a per-frame scan of every sprite entity the client had ever received,
        // which cost ~2 ms a frame on Nebula and grew the longer the client was connected.
        EntityManager.EntityInitialized += OnEntityInitialized;
        RebuildArcadePrototypes();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.EntityInitialized -= OnEntityInitialized;
    }

    private void OnEntityInitialized(Entity<MetaDataComponent> ent)
    {
        if (ent.Comp.EntityPrototype is { } proto && _arcadePrototypes.Contains(proto.ID))
            EnsureComp<WebArcadeComponent>(ent);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        RebuildArcadePrototypes();

        var query = AllEntityQuery<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            OnEntityInitialized((uid, meta));
        }
    }

    private void RebuildArcadePrototypes()
    {
        _arcadePrototypes.Clear();
        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            // Abstract ancestors are skipped by EnumerateParents, so look for ArcadeBase among
            // the direct parents of each concrete ancestor (and the prototype itself).
            foreach (var ancestor in _prototype.EnumerateParents<EntityPrototype>(proto, includeSelf: true))
            {
                if (ancestor.Parents == null || Array.IndexOf(ancestor.Parents, ArcadeBaseId) < 0)
                    continue;

                _arcadePrototypes.Add(proto.ID);
                break;
            }
        }
    }

    private void OnGetVerbs(Entity<WebArcadeComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Гральний автомат",
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/buckle.svg.192dpi.png")),
            Act = () =>
            {
                if (!DebounceVerify())
                    return;
                if (WebArcadeWindow.TryGetOpen(NetEntityWithContext(entity), out _))
                    return; // already in a window on this cabinet
                var window = new WebArcadeWindow();
                window.CabUid = entity;
                window.OpenCenteredArcade();
            },
            Priority = 12,
        });
    }

    private static Robust.Shared.GameObjects.NetEntity NetEntityWithContext(Entity<WebArcadeComponent> entity)
    {
        return Robust.Shared.IoC.IoCManager
            .Resolve<Robust.Shared.GameObjects.IEntityManager>().GetNetEntity(entity);
    }

    private static long _lastWindowStamp;

    private static bool DebounceVerify()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - _lastWindowStamp < 500)
            return false;
        _lastWindowStamp = now;
        return true;
    }
}
