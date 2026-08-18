using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Server.Preferences.Managers;
using Content.Server.RandomMetadata;
using Content.Server.RoundEnd;
using Content.Server._Pirate.Character.Info;
using Content.Server._Pirate.Traits;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tag;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Mercenary
{
    public sealed class MakeAMercSystem : EntitySystem
    {
        private const string MapPath = "Maps/_Pirate/Shuttles/Qazmlp/st_merc.yml";

        [ValidatePrototypeId<RandomHumanoidSettingsPrototype>]
        private const string SpawnerPrototypeId = "Mercenary";

        [ValidatePrototypeId<EntityPrototype>] private const string Disk = "CoordinatesDisk";

        [ValidatePrototypeId<TagPrototype>] private const string ShuttleTag = "Syndicate";

        private static readonly string[] ShuttleNames =
        {
            "Courier",
            "Maria",
            "Midnight Range",
            "Searchlight",
        };

        [Dependency] private readonly IEntityManager _entManager = default!;
        [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;
        [Dependency] private readonly MapLoaderSystem _map = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly RandomHumanoidSystem _randomHumanoidSystem = default!;
        [Dependency] private readonly ISerializationManager _serialization = default!;
        [Dependency] private readonly PirateCharacterInfoSystem _characterInfoSystem = default!;
        [Dependency] private readonly TraitSystem _traitSystem = default!;
        [Dependency] private readonly RandomMetadataSystem _randomMetadataSystem = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;
        [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        private EntityUid? _mercBaseGrid;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        }

        private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
        {
            _mercBaseGrid = null;
        }

        public void MakeAnMerc(EntityUid entity)
        {
            _playerManager.TryGetSessionByEntity(entity, out var session);

            if (session is null)
                return;

            var playerCData = session.ContentData();
            if (playerCData == null)
                return;

            if (!TryGetMercBaseGrid(out var shuttle))
                return;

            // Ensure the base remains a disk-locked FTL destination.
            var mercMapUid = Transform(shuttle).MapUid;
            if (mercMapUid == null)
                return;

            var mindSystem = _entManager.System<SharedMindSystem>();
            var metadata = _entManager.GetComponent<MetaDataComponent>(entity);
            var mind = playerCData.Mind ?? mindSystem.CreateMind(session.UserId, metadata.EntityName);

            var destinationComponent = EnsureComp<FTLDestinationComponent>(mercMapUid.Value);
            destinationComponent.Enabled = true;
            destinationComponent.RequireCoordinateDisk = true;
            _entManager.Dirty(mercMapUid.Value, destinationComponent);

            var spawn = TryGetMercSpawnPoint(mercMapUid.Value, out var markerCoords)
                ? markerCoords
                : Transform(shuttle).Coordinates;

            var uid = SpawnMercBody(entity, session, spawn, out var mercProfile);
            RemComp<GhostRoleComponent>(uid);
            mindSystem.TransferTo(mind, uid, true);

            // Apply profile data without the normal spawn side effects.
            if (mercProfile != null)
            {
                _traitSystem.ApplyProfileTraits(uid, mercProfile, session, null);
                _characterInfoSystem.ApplyCharacterInfo(uid, mercProfile);
            }

            var disk = EntityManager.SpawnEntity(Disk, spawn);
            var cd = _entManager.EnsureComponent<ShuttleDestinationCoordinatesComponent>(disk);
            cd.Destination = mercMapUid.Value;
            _entManager.Dirty(disk, cd);
        }

        private EntityUid SpawnMercBody(EntityUid source, ICommonSession session, EntityCoordinates coordinates,
            out HumanoidCharacterProfile? profile)
        {
            var settings = _prototypeManager.Index<RandomHumanoidSettingsPrototype>(SpawnerPrototypeId);

            if (_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs)
                && prefs.SelectedCharacter is HumanoidCharacterProfile selected
                && _prototypeManager.Resolve(selected.Species, out var species))
            {
                var body = CreateMercBody(species.Prototype, coordinates, settings, selected.Name);
                _humanoidSystem.LoadProfile(body, selected);
                _entManager.InitializeAndStartEntity(body);

                Log.Info($"makemerc: used the selected character of {session.Name} ({selected.Name}, {species.ID}).");

                profile = selected;
                return body;
            }

            Log.Info($"makemerc: no selected character for {session.Name}, rolling a random merc.");

            profile = null;
            return _randomHumanoidSystem.SpawnRandomHumanoid(SpawnerPrototypeId, coordinates, MetaData(source).EntityName);
        }

        private EntityUid CreateMercBody(EntProtoId speciesEntity, EntityCoordinates coordinates,
            RandomHumanoidSettingsPrototype settings, string name)
        {
            var body = _entManager.CreateEntityUninitialized(speciesEntity, coordinates);

            _metaDataSystem.SetEntityName(body, name);

            if (settings.Components != null)
            {
                foreach (var entry in settings.Components.Values)
                {
                    var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
                    RemComp(body, comp.GetType());
                    AddComp(body, comp);
                }
            }

            return body;
        }

        private bool TryGetMercBaseGrid(out EntityUid grid)
        {
            if (_mercBaseGrid is { } cached && !TerminatingOrDeleted(cached))
            {
                grid = cached;
                return true;
            }

            var options = MapLoadOptions.Default with
            {
                DeserializationOptions = DeserializationOptions.Default with {InitializeMaps = true}
            };

            if (!_map.TryLoadGeneric(new ResPath(MapPath), out var result, options))
            {
                grid = default;
                return false;
            }

            var shuttle = result?.Grids?.FirstOrNull(g => _tagSystem.HasTag(g, ShuttleTag));
            if (shuttle == null)
            {
                grid = default;
                return false;
            }

            _metaDataSystem.SetEntityName(shuttle.Value, _random.Pick(ShuttleNames));

            _mercBaseGrid = shuttle.Value;
            grid = shuttle.Value;
            return true;
        }

        private bool TryGetMercSpawnPoint(EntityUid mapUid, out EntityCoordinates coordinates)
        {
            var query = EntityQueryEnumerator<MercSpawnPointComponent, TransformComponent>();
            while (query.MoveNext(out _, out _, out var xform))
            {
                if (xform.MapUid != mapUid)
                    continue;

                coordinates = xform.Coordinates;
                return true;
            }

            coordinates = default;
            return false;
        }
    }
}
