// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Components;
using Content.Shared.Access.Systems;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
#region Pirate: records photos
using Content.Shared._Pirate.Origin;
using Content.Shared._Pirate.Employment;
using Content.Shared.CriminalRecords;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Server._Pirate.StationRecords; // Pirate: records photos
using Content.Shared._Pirate.StationRecords; // Pirate: records photos
#endregion

namespace Content.Server.StationRecords.Systems;

public sealed class GeneralStationRecordConsoleSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    #region Pirate: records photos
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RecordPortraitSystem _portraits = default!; // Pirate: records photos
    [Dependency] private readonly RecordLogSystem _logs = default!; // Pirate: record history
    private static readonly IReadOnlyDictionary<string, TimeSpan> EmptyPlayTimes = new Dictionary<string, TimeSpan>();
    #endregion

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordRemovedEvent>(UpdateUserInterface);

        Subs.BuiEvents<GeneralStationRecordConsoleComponent>(GeneralStationRecordConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            #region Pirate: records photos
            subs.Event<GeneralRecordCreateRecord>(OnCreateRecord);
            subs.Event<GeneralRecordEditIdentity>(OnEditIdentity);
            subs.Event<GeneralRecordEditForensics>(OnEditForensics);
            #endregion
            #region Pirate: records photos
            subs.Event<RecordPortraitPrintMessage>(OnPrintPortrait);
            subs.Event<RecordPortraitUploadMessage>(OnUploadPortrait);
            subs.Event<RecordPortraitStoreGeneratedMessage>(OnStoreGeneratedPortrait);
            subs.Event<RecordLogAddNoteMessage>(OnAddLogNote);
            subs.Event<RecordLogDeleteMessage>(OnDeleteLogEntry);
            subs.Event<RecordLogPrintMessage>(OnPrintLog);
            #endregion
            subs.Event<DeleteStationRecord>(OnRecordDelete);
        });
    }

    private void OnRecordDelete(Entity<GeneralStationRecordConsoleComponent> ent, ref DeleteStationRecord args)
    {
        if (!CanManageRecords(ent) || !_access.IsAllowed(args.Actor, ent)) // Pirate: records photos
            return;

        var owning = _station.GetOwningStation(ent.Owner);
        #region Pirate: records photos
        if (owning == null)
            return;

        var key = new StationRecordKey(args.Id, owning.Value);
        if (!_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var generalRecord))
            return;

        if (_stationRecords.TryGetRecord<CriminalRecord>(key, out var criminalRecord))
        {
            criminalRecord.GeneralRecordSnapshot = generalRecord with { };
            _stationRecords.RemoveRecordEntry<GeneralStationRecord>(key);
        }
        else
        {
            _stationRecords.RemoveRecord(key);
        }

        if (ent.Comp.ActiveKey == args.Id)
            ent.Comp.ActiveKey = null;

        UpdateUserInterface(ent);
        #endregion
    }

    private void UpdateUserInterface<T>(Entity<GeneralStationRecordConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    // TODO: instead of copy paste shitcode for each record console, have a shared records console comp they all use
    // then have this somehow play nicely with creating ui state
    // if that gets done put it in StationRecordsSystem console helpers section :)
    private void OnKeySelected(Entity<GeneralStationRecordConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        ent.Comp.SelectionRequest = msg.Request; // Pirate: records select
        UpdateUserInterface(ent);
    }

    private void OnFiltersChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    #region Pirate: records photos
    private void OnCreateRecord(Entity<GeneralStationRecordConsoleComponent> ent, ref GeneralRecordCreateRecord msg)
    {
        if (!CanManageRecords(ent) || !_access.IsAllowed(msg.Actor, ent)) // Pirate: records photos
            return;

        if (_station.GetOwningStation(ent) is not { } station ||
            !TryComp<StationRecordsComponent>(station, out var stationRecords))
        {
            return;
        }

        var name = msg.Name.Trim();
        if (name.Length > ent.Comp.MaxStringLength)
            name = name[..(int) ent.Comp.MaxStringLength];

        if (string.IsNullOrWhiteSpace(name))
            return;

        var generalMatches = _stationRecords.GetRecordIdsByName(station, name, stationRecords);
        if (generalMatches.Count > 1)
            return;

        if (generalMatches.Count == 1)
        {
            ent.Comp.ActiveKey = generalMatches[0];
            UpdateUserInterface(ent);
            return;
        }

        var criminalMatches = _stationRecords.GetRecordsOfType<CriminalRecord>(station, stationRecords)
            .Where(pair => !_stationRecords.TryGetRecord<GeneralStationRecord>(new StationRecordKey(pair.Item1, station), out _)
                && pair.Item2.GeneralRecordSnapshot?.Name == name)
            .ToList();

        if (criminalMatches.Count > 1)
            return;

        StationRecordKey key;
        if (criminalMatches.Count == 1)
        {
            var (recordId, criminalRecord) = criminalMatches[0];
            key = new StationRecordKey(recordId, station);
            var generalRecord = criminalRecord.GeneralRecordSnapshot is { } snapshot
                ? snapshot with { }
                : CreateDefaultGeneralRecord(name);

            _stationRecords.AddRecordEntry(key, generalRecord, stationRecords);
            criminalRecord.GeneralRecordSnapshot = generalRecord with { };
            _stationRecords.Synchronize(key, stationRecords);
        }
        else
        {
            var generalRecord = CreateDefaultGeneralRecord(name);
            key = _stationRecords.AddRecordEntry(station, generalRecord, stationRecords);
            if (!key.IsValid())
                return;

            _stationRecords.Synchronize(key, stationRecords);
        }

        ent.Comp.ActiveKey = key.Id;
        UpdateUserInterface(ent);
    }

    private void OnEditIdentity(Entity<GeneralStationRecordConsoleComponent> ent, ref GeneralRecordEditIdentity msg)
    {
        if (!CanManageRecords(ent) || !_access.IsAllowed(msg.Actor, ent)) // Pirate: records photos
            return;

        if (_station.GetOwningStation(ent) is not { } station || ent.Comp.ActiveKey is not { } id)
            return;

        var key = new StationRecordKey(id, station);
        if (!_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var generalRecord))
            return;

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(msg.Species, out var speciesProto)
            || !speciesProto.RoundStart)
        {
            return;
        }

        _stationRecords.TryGetRecord<CriminalRecord>(key, out var criminalRecord);

        var age = Math.Clamp(msg.Age, speciesProto.MinAge, speciesProto.MaxAge);
        var profile = BuildEditedProfile(generalRecord, _portraits.GetSnapshot(key), speciesProto.ID, age, msg.Gender);
        var citizenship = NormalizeCitizenship(msg.Citizenship, profile);
        profile = profile.WithCitizenship(citizenship);
        var employer = NormalizeEmployer(msg.Employer, profile);
        profile = profile.WithEmployer(employer);

        generalRecord.Species = speciesProto.ID;
        generalRecord.Age = age;
        generalRecord.Gender = msg.Gender;
        generalRecord.Citizenship = citizenship;
        generalRecord.Employer = employer;

        if (criminalRecord != null)
            criminalRecord.GeneralRecordSnapshot = generalRecord with { };

        _portraits.UpdateSnapshot(key, profile);
        _stationRecords.Synchronize(key);
    }

    private void OnEditForensics(Entity<GeneralStationRecordConsoleComponent> ent, ref GeneralRecordEditForensics msg)
    {
        if (!CanManageRecords(ent) || !_access.IsAllowed(msg.Actor, ent)) // Pirate: records photos
            return;

        if (_station.GetOwningStation(ent) is not { } station || ent.Comp.ActiveKey is not { } id)
            return;

        var key = new StationRecordKey(id, station);
        if (!_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var generalRecord))
            return;

        var fingerprint = msg.Fingerprint.Trim();
        var dna = msg.Dna.Trim();
        if (fingerprint.Length > ent.Comp.MaxStringLength || dna.Length > ent.Comp.MaxStringLength)
            return;

        generalRecord.Fingerprint = string.IsNullOrWhiteSpace(fingerprint)
            ? null
            : fingerprint;
        generalRecord.DNA = string.IsNullOrWhiteSpace(dna)
            ? null
            : dna;

        if (_stationRecords.TryGetRecord<CriminalRecord>(key, out var criminalRecord))
            criminalRecord.GeneralRecordSnapshot = generalRecord with { };

        _stationRecords.Synchronize(key);
    }

    private GeneralStationRecord CreateDefaultGeneralRecord(string name)
    {
        return new GeneralStationRecord
        {
            Name = name,
            Age = 18,
            JobTitle = Loc.GetString("suit-sensor-component-unknown-job"),
            JobIcon = string.Empty,
            JobPrototype = string.Empty,
            Citizenship = string.Empty,
            Employer = string.Empty,
            Species = string.Empty,
            Gender = Gender.Male,
            DisplayPriority = 0,
            Fingerprint = null,
            DNA = null,
        };
    }

    private HumanoidCharacterProfile BuildEditedProfile(
        GeneralStationRecord generalRecord,
        HumanoidCharacterProfile? portraitSnapshot,
        string species,
        int age,
        Gender gender)
    {
        var sameSpeciesSnapshot = portraitSnapshot != null && portraitSnapshot.Species == species;

        var profile = sameSpeciesSnapshot
            ? new HumanoidCharacterProfile(portraitSnapshot!)
            : HumanoidCharacterProfile.DefaultWithSpecies(species).WithName(generalRecord.Name);

        return profile
            .WithSpecies(species)
            .WithAge(age)
            .WithGender(gender)
            .WithCitizenship(generalRecord.Citizenship)
            .WithEmployer(generalRecord.Employer);
    }

    private string NormalizeCitizenship(string requestedCitizenship, HumanoidCharacterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(requestedCitizenship))
            return string.Empty;

        if (_prototypeManager.TryIndex<CitizenshipPrototype>(requestedCitizenship, out var citizenship)
            && RequirementsMet(citizenship.Requirements, profile.WithCitizenship(requestedCitizenship)))
        {
            return requestedCitizenship;
        }

        if (_prototypeManager.TryIndex<CitizenshipPrototype>(SharedHumanoidAppearanceSystem.DefaultCitizenship, out var defaultCitizenship)
            && RequirementsMet(defaultCitizenship.Requirements, profile.WithCitizenship(defaultCitizenship.ID)))
        {
            return defaultCitizenship.ID;
        }

        foreach (var prototype in _prototypeManager.EnumeratePrototypes<CitizenshipPrototype>())
        {
            if (RequirementsMet(prototype.Requirements, profile.WithCitizenship(prototype.ID)))
                return prototype.ID;
        }

        return SharedHumanoidAppearanceSystem.DefaultCitizenship;
    }

    private string NormalizeEmployer(string requestedEmployer, HumanoidCharacterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(requestedEmployer))
            return string.Empty;

        if (_prototypeManager.TryIndex<EmployerPrototype>(requestedEmployer, out var employer)
            && RequirementsMet(employer.Requirements, profile.WithEmployer(requestedEmployer)))
        {
            return requestedEmployer;
        }

        if (_prototypeManager.TryIndex<EmployerPrototype>(SharedHumanoidAppearanceSystem.DefaultEmployer, out var defaultEmployer)
            && RequirementsMet(defaultEmployer.Requirements, profile.WithEmployer(defaultEmployer.ID)))
        {
            return defaultEmployer.ID;
        }

        foreach (var prototype in _prototypeManager.EnumeratePrototypes<EmployerPrototype>())
        {
            if (RequirementsMet(prototype.Requirements, profile.WithEmployer(prototype.ID)))
                return prototype.ID;
        }

        return SharedHumanoidAppearanceSystem.DefaultEmployer;
    }

    private bool RequirementsMet(IReadOnlyCollection<JobRequirement>? requirements, HumanoidCharacterProfile profile)
    {
        if (requirements == null || requirements.Count == 0)
            return true;

        foreach (var requirement in requirements)
        {
            if (!requirement.Check(EntityManager, _prototypeManager, profile, EmptyPlayTimes, out _))
                return false;
        }

        return true;
    }

    #region Pirate: records photos
    private bool TryGetPortraitTarget(Entity<GeneralStationRecordConsoleComponent> ent, EntityUid actor, out StationRecordKey key)
    {
        key = default;
        if (!CanManageRecords(ent) || !_access.IsAllowed(actor, ent))
            return false;

        if (_station.GetOwningStation(ent) is not { } station || ent.Comp.ActiveKey is not { } id)
            return false;

        key = new StationRecordKey(id, station);
        return true;
    }

    private void OnPrintPortrait(Entity<GeneralStationRecordConsoleComponent> ent, ref RecordPortraitPrintMessage msg)
    {
        if (TryGetPortraitTarget(ent, msg.Actor, out var key) && key.Id == msg.RecordKey)
            _portraits.Print(ent, msg.Actor, key, msg.ImageData);
    }

    private void OnUploadPortrait(Entity<GeneralStationRecordConsoleComponent> ent, ref RecordPortraitUploadMessage msg)
    {
        if (TryGetPortraitTarget(ent, msg.Actor, out var key) && _portraits.Upload(ent, msg.Actor, key))
            UpdateUserInterface(ent);
    }

    private void OnAddLogNote(Entity<GeneralStationRecordConsoleComponent> ent, ref RecordLogAddNoteMessage msg)
    {
        var text = RecordLogNotes.Sanitize(msg.Text);
        if (text.Length < 1 || text.Length > RecordLogNotes.MaxLength || !TryGetPortraitTarget(ent, msg.Actor, out var key))
            return;

        _logs.Add(key, RecordLogBook.Crew, RecordLogKind.Note, null, text, msg.Actor, ent);
        UpdateUserInterface(ent);
    }

    private void OnDeleteLogEntry(Entity<GeneralStationRecordConsoleComponent> ent, ref RecordLogDeleteMessage msg)
    {
        if (TryGetPortraitTarget(ent, msg.Actor, out var key) && _logs.TryDelete(key, RecordLogBook.Crew, msg.EntryId, out _))
            UpdateUserInterface(ent);
    }

    private void OnPrintLog(Entity<GeneralStationRecordConsoleComponent> ent, ref RecordLogPrintMessage msg)
    {
        if (TryGetPortraitTarget(ent, msg.Actor, out var key))
            _logs.Print(ent, msg.Actor, key, RecordLogBook.Crew);
    }

    private void OnStoreGeneratedPortrait(Entity<GeneralStationRecordConsoleComponent> ent, ref RecordPortraitStoreGeneratedMessage msg)
    {
        // Pirate: records photos - Accept late render results for their original record.
        if (!_access.IsAllowed(msg.Actor, ent) || _station.GetOwningStation(ent) is not { } station)
            return;

        if (_portraits.StoreGenerated(new StationRecordKey(msg.RecordKey, station), msg.ImageData)
            && ent.Comp.ActiveKey == msg.RecordKey)
        {
            UpdateUserInterface(ent);
        }
    }
    #endregion

    private bool CanManageRecords(Entity<GeneralStationRecordConsoleComponent> ent) // Pirate: records photos
    {
        return _access.GetMainAccessReader(ent.Owner, out _) || ent.Comp.CanDeleteEntries; // Pirate: records photos
    }
    #endregion

    private void UpdateUserInterface(Entity<GeneralStationRecordConsoleComponent> ent)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        if (!TryComp<StationRecordsComponent>(owningStation, out var stationRecords))
        {
            _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, // Pirate: records photos
                new GeneralStationRecordConsoleState(null, null, null, console.Filter, CanManageRecords(ent), ent.Comp.MaxStringLength)); // Pirate: records photos
            return;
        }

        var listing = _stationRecords.BuildListing((owningStation.Value, stationRecords), console.Filter);

        switch (listing.Count)
        {
            case 0:
                _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key,  // Pirate: records photos
                    new GeneralStationRecordConsoleState(null, null, null, console.Filter, CanManageRecords(ent), ent.Comp.MaxStringLength));  // Pirate: records photos
                return;
            default:
                if (console.ActiveKey == null)
                    console.ActiveKey = listing.Keys.First();
                break;
        }

        if (console.ActiveKey is not { } id)
            return;

        var key = new StationRecordKey(id, owningStation.Value);
        _stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords);

        #region Pirate: records photos
        if (record == null)
            console.ActiveKey = null;

        var newState = new GeneralStationRecordConsoleState(console.ActiveKey, record, listing, console.Filter,
            CanManageRecords(ent), ent.Comp.MaxStringLength); // Pirate: records photos
        newState.SelectionRequest = console.SelectionRequest; // Pirate: records select
        if (record != null) // Pirate: records photos
        {
            newState.Portrait = _portraits.GetPortrait(key); // Pirate: records photos
            newState.Log = _logs.GetLog(key, RecordLogBook.Crew); // Pirate: record history
        }
        #endregion
        _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, newState);
    }
}
