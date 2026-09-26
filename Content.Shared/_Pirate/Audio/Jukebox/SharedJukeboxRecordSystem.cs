// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Audio.Jukebox;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Audio.Jukebox;

public sealed class SharedJukeboxRecordSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JukeboxRecordsComponent, ComponentInit>(OnRecordsInit);
        SubscribeLocalEvent<JukeboxRecordsComponent, InteractUsingEvent>(OnRecordsInteractUsing);
        SubscribeLocalEvent<JukeboxRecordsComponent, GetVerbsEvent<AlternativeVerb>>(OnRecordsGetVerbs);
        SubscribeLocalEvent<JukeboxRecordsComponent, EntInsertedIntoContainerMessage>(OnRecordInserted);
        SubscribeLocalEvent<JukeboxRecordsComponent, EntRemovedFromContainerMessage>(OnRecordRemoved);
        SubscribeLocalEvent<JukeboxRecordsComponent, JukeboxSelectRecordMessage>(OnRecordSelected);
        SubscribeLocalEvent<JukeboxRecordsComponent, JukeboxEjectRecordMessage>(OnEjectRecordMessage);

        SubscribeLocalEvent<JukeboxRecordComponent, ExaminedEvent>(OnRecordExamined);
    }

    private void OnRecordsInit(Entity<JukeboxRecordsComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
    }

    private void OnRecordExamined(Entity<JukeboxRecordComponent> ent, ref ExaminedEvent args)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.Path))
        {
            args.PushMarkup(Loc.GetString("jukebox-record-examine-blank"));
            return;
        }

        args.PushMarkup(Loc.GetString("jukebox-record-examine", ("track", GetTrackName(ent))));

        if (!IsTrackAvailable(ent))
            args.PushMarkup(Loc.GetString("jukebox-record-examine-missing", ("path", ent.Comp.Path)));
    }

    private void OnRecordsInteractUsing(Entity<JukeboxRecordsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<JukeboxRecordComponent>(args.Used, out var record))
            return;

        args.Handled = true;

        if (string.IsNullOrWhiteSpace(record.Path))
        {
            _popup.PopupClient(Loc.GetString("jukebox-record-blank"), ent, args.User);
            return;
        }

        if (!IsTrackAvailable((args.Used, record)))
        {
            _popup.PopupClient(Loc.GetString("jukebox-record-missing"), ent, args.User);
            Log.Warning($"{ToPrettyString(args.Used)} points at '{record.Path}', which is not in the VFS. Upload it with uploadfolder first; the path must be rooted, e.g. /Uploaded/music/track.ogg");
            return;
        }

        if (!_container.Insert(args.Used, ent.Comp.Container))
            return;

        _audio.PlayPredicted(ent.Comp.InsertSound, ent, args.User);
        _popup.PopupClient(Loc.GetString("jukebox-record-inserted", ("track", GetTrackName((args.Used, record)))), ent, args.User);
    }

    private void OnRecordsGetVerbs(Entity<JukeboxRecordsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Container.Count == 0)
            return;

        var user = args.User;
        var records = GetRecords(ent.AsNullable());

        foreach (var record in records)
        {
            var uid = record.Owner;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = GetTrackName(record),
                Category = VerbCategory.Eject,
                IconEntity = GetNetEntity(uid),
                Act = () => EjectRecord(ent, uid, user),
            });
        }

        // Offer Eject all for multiple or unplayable records.
        if (ent.Comp.Container.Count < 2 && records.Count == ent.Comp.Container.Count)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("jukebox-records-eject-all-verb"),
            Category = VerbCategory.Eject,
            Act = () => EjectRecords(ent, user),
            Priority = 1,
        });
    }

    private void OnEjectRecordMessage(Entity<JukeboxRecordsComponent> ent, ref JukeboxEjectRecordMessage args)
    {
        EjectRecord(ent, GetEntity(args.Record), args.Actor);
    }

    public bool EjectRecord(Entity<JukeboxRecordsComponent> ent, EntityUid record, EntityUid? user = null)
    {
        if (!ent.Comp.Container.Contains(record))
            return false;

        if (!_container.Remove(record, ent.Comp.Container))
            return false;

        if (user != null)
            _hands.TryPickupAnyHand(user.Value, record);

        _audio.PlayPredicted(ent.Comp.EjectSound, ent, user);
        return true;
    }

    public void EjectRecords(Entity<JukeboxRecordsComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Container.Count == 0)
            return;

        _container.EmptyContainer(ent.Comp.Container, destination: Transform(ent).Coordinates);
        _audio.PlayPredicted(ent.Comp.EjectSound, ent, user);
    }

    private void OnRecordInserted(Entity<JukeboxRecordsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        RaiseRecordsChanged(ent);
    }

    // Stop playback if the selected record is removed.
    private void OnRecordRemoved(Entity<JukeboxRecordsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        if (TryComp<JukeboxComponent>(ent, out var jukebox) && jukebox.SelectedRecord == args.Entity)
            StopSelectedRecord((ent.Owner, jukebox));

        RaiseRecordsChanged(ent);
    }

    private void RaiseRecordsChanged(EntityUid uid)
    {
        var ev = new JukeboxRecordsChangedEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnRecordSelected(Entity<JukeboxRecordsComponent> ent, ref JukeboxSelectRecordMessage args)
    {
        SelectRecord(ent, GetEntity(args.Record));
    }

    public void SelectRecord(Entity<JukeboxRecordsComponent> ent, EntityUid record)
    {
        if (!TryComp<JukeboxComponent>(ent, out var jukebox))
            return;

        if (!HasComp<JukeboxRecordComponent>(record) || !ent.Comp.Container.Contains(record))
            return;

        var wasPlaying = _audio.IsPlaying(jukebox.AudioStream);

        jukebox.SelectedSongId = null;
        jukebox.SelectedRecord = record;
        jukebox.AudioStream = _audio.Stop(jukebox.AudioStream);
        jukebox.FirstPlay = true;
        jukebox.Selecting = true;
        jukebox.SelectAccumulator = 0f;
        _appearance.SetData(ent, JukeboxVisuals.VisualState, JukeboxVisualState.Select);

        if (wasPlaying)
            TryPlayRecord((ent.Owner, jukebox));

        Dirty(ent.Owner, jukebox);
    }

    public bool TryPlayRecord(Entity<JukeboxComponent> ent)
    {
        if (_net.IsClient)
            return false;

        if (!TryGetSelectedTrack(ent, out var path, out _))
            return false;

        if (ent.Comp.SelectedRecord is { } selected &&
            TryComp<JukeboxRecordComponent>(selected, out var selectedRecord) &&
            !IsTrackAvailable((selected, selectedRecord)))
        {
            Log.Warning($"{ToPrettyString(ent)} tried to play '{path}', which is not in the VFS. Was the audio uploaded with uploadfolder?");
            return false;
        }

        ent.Comp.AudioStream = _audio.Stop(ent.Comp.AudioStream);
        ent.Comp.AudioStream = _audio.PlayPvs(new ResolvedPathSpecifier(path),
            ent.Owner,
            AudioParams.Default.WithMaxDistance(10f).WithVolume(-6f))?.Entity;
        ent.Comp.FirstPlay = false;

        Dirty(ent);
        return true;
    }

    private void StopSelectedRecord(Entity<JukeboxComponent> ent)
    {
        ent.Comp.SelectedRecord = null;
        ent.Comp.AudioStream = _audio.Stop(ent.Comp.AudioStream);
        ent.Comp.FirstPlay = true;
        Dirty(ent);
    }

    public bool TryGetSelectedTrack(Entity<JukeboxComponent> ent, out string path, out string name)
    {
        path = string.Empty;
        name = string.Empty;

        if (ent.Comp.SelectedRecord is not { } record ||
            !TryComp<JukeboxRecordComponent>(record, out var recordComp) ||
            string.IsNullOrWhiteSpace(recordComp.Path))
        {
            return false;
        }

        path = recordComp.Path;
        name = GetTrackName((record, recordComp));
        return true;
    }

    // Returns playable records sorted by track name.
    public List<Entity<JukeboxRecordComponent>> GetRecords(Entity<JukeboxRecordsComponent?> ent)
    {
        var records = new List<Entity<JukeboxRecordComponent>>();

        if (!Resolve(ent, ref ent.Comp, false) ||
            !_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
        {
            return records;
        }

        foreach (var contained in container.ContainedEntities)
        {
            if (TryComp<JukeboxRecordComponent>(contained, out var record) && !string.IsNullOrWhiteSpace(record.Path))
                records.Add((contained, record));
        }

        records.Sort((a, b) => string.Compare(GetTrackName(a), GetTrackName(b), StringComparison.CurrentCulture));
        return records;
    }

    public bool TryPickRandomRecord(Entity<JukeboxRecordsComponent?> ent, out EntityUid record)
    {
        record = default;
        var records = GetRecords(ent);

        if (records.Count == 0)
            return false;

        record = _random.Pick(records).Owner;
        return true;
    }

    public bool IsTrackAvailable(Entity<JukeboxRecordComponent> record)
    {
        return TryGetTrackPath(record.Comp.Path, out var resPath) && _resources.ContentFileExists(resPath);
    }

    // Validate before constructing ResPath to avoid assertions for malformed YAML paths.
    private static bool TryGetTrackPath(string? path, out ResPath resPath)
    {
        resPath = default;

        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || !ResPath.IsValidPath(path))
            return false;

        resPath = new ResPath(path);
        return true;
    }

    public string GetTrackName(Entity<JukeboxRecordComponent> record)
    {
        if (!string.IsNullOrWhiteSpace(record.Comp.Title))
            return record.Comp.Title;

        return TryGetTrackPath(record.Comp.Path, out var resPath)
            ? resPath.FilenameWithoutExtension
            : Loc.GetString("jukebox-record-unknown-track");
    }
}
