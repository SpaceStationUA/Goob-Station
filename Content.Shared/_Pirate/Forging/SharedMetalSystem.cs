// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Forging;

/// <summary>
/// Event-driven metal state API. Prototype caches are rebuilt only at startup or prototype reload.
/// </summary>
public abstract class SharedMetalSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private EntityQuery<MetallicComponent> _metallicQuery;

    public readonly List<MetalPrototype> AllMetals = new();

    public override void Initialize()
    {
        base.Initialize();

        _metallicQuery = GetEntityQuery<MetallicComponent>();
        SubscribeLocalEvent<MetallicComponent, MetalChangedEvent>(OnMetalChanged);
        SubscribeLocalEvent<MetallicPopupsComponent, MetalWorkableChangedEvent>(OnPopupChanged);
        SubscribeLocalEvent<MetallicTagsComponent, MetalWorkableChangedEvent>(OnTagsChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadPrototypes();
    }

    private void OnMetalChanged(Entity<MetallicComponent> ent, ref MetalChangedEvent args)
    {
        if (ent.Comp.MinTemp != 0)
            return;

        ent.Comp.MinTemp = args.Metal.MinTemp;
        ent.Comp.IdealTemp = args.Metal.WorkingTemp;
        DirtyField(ent, ent.Comp, nameof(MetallicComponent.MinTemp));
        DirtyField(ent, ent.Comp, nameof(MetallicComponent.IdealTemp));
    }

    private void OnPopupChanged(Entity<MetallicPopupsComponent> ent, ref MetalWorkableChangedEvent args)
    {
        var message = args.Workable ? ent.Comp.HeatedPopup : ent.Comp.CooledPopup;
        _popup.PopupEntity(Loc.GetString(message, ("name", ent.Owner)), ent.Owner);
    }

    private void OnTagsChanged(Entity<MetallicTagsComponent> ent, ref MetalWorkableChangedEvent args)
    {
        if (args.Workable)
        {
            _tags.AddTags(ent.Owner, ent.Comp.Workable);
            _tags.RemoveTags(ent.Owner, ent.Comp.Unworkable);
            return;
        }

        _tags.AddTags(ent.Owner, ent.Comp.Unworkable);
        _tags.RemoveTags(ent.Owner, ent.Comp.Workable);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<MetalPrototype>())
            LoadPrototypes();
    }

    private void LoadPrototypes()
    {
        AllMetals.Clear();
        AllMetals.AddRange(_prototypes.EnumeratePrototypes<MetalPrototype>());
        AllMetals.Sort((first, second) => string.Compare(first.Name, second.Name, StringComparison.Ordinal));
    }

    public void SetWorkable(Entity<MetallicComponent> ent, bool workable)
    {
        if (ent.Comp.Workable == workable)
            return;

        ent.Comp.Workable = workable;
        DirtyField(ent, ent.Comp, nameof(MetallicComponent.Workable));
        var ev = new MetalWorkableChangedEvent(workable);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    public bool IsWorkable(EntityUid uid)
        => _metallicQuery.CompOrNull(uid)?.Workable ?? false;

    public bool TryGetMetal(EntityUid uid, out ProtoId<MetalPrototype> metal)
    {
        if (_metallicQuery.TryComp(uid, out var component) && component.Metal is { } id)
        {
            metal = id;
            return true;
        }

        metal = default;
        return false;
    }

    public ProtoId<MetalPrototype> GetMetalOrThrow(EntityUid uid)
        => _metallicQuery.Comp(uid).Metal!.Value;

    public void SetMetal(Entity<MetallicComponent?> ent, ProtoId<MetalPrototype> metal)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Metal == metal || !_prototypes.Resolve(metal, out var prototype))
            return;

        ent.Comp.Metal = metal;
        DirtyField(ent, ent.Comp, nameof(MetallicComponent.Metal));
        var ev = new MetalChangedEvent(prototype);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    public bool AddWorkableTag(Entity<MetallicTagsComponent?> ent, ProtoId<TagPrototype> tag)
    {
        ent.Comp ??= EnsureComp<MetallicTagsComponent>(ent.Owner);
        if (ent.Comp.Workable.Contains(tag))
            return false;

        ent.Comp.Workable.Add(tag);
        Dirty(ent.Owner, ent.Comp);
        if (IsWorkable(ent.Owner))
            _tags.AddTag(ent.Owner, tag);
        return true;
    }

    public bool AddUnworkableTag(Entity<MetallicTagsComponent?> ent, ProtoId<TagPrototype> tag)
    {
        ent.Comp ??= EnsureComp<MetallicTagsComponent>(ent.Owner);
        if (ent.Comp.Unworkable.Contains(tag))
            return false;

        ent.Comp.Unworkable.Add(tag);
        Dirty(ent.Owner, ent.Comp);
        if (!IsWorkable(ent.Owner))
            _tags.AddTag(ent.Owner, tag);
        return true;
    }

    public virtual void SetPrice(EntityUid uid, double price)
    {
    }
}
