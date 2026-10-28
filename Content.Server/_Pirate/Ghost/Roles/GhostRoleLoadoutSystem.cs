// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Preferences.Managers;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Station;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Ghost.Roles;

public sealed class GhostRoleLoadoutSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedStationSpawningSystem _spawning = default!;
    [Dependency] private readonly LoadoutSystem _loadouts = default!;

    public void EquipLoadout(EntityUid mob, ICommonSession player, EntityUid? spawner = null)
    {
        if (TryComp<GhostRoleLoadoutComponent>(mob, out var bodyLoadout) && bodyLoadout.Applied)
            return;

        var settings = CompOrNull<GhostRoleLoadoutComponent>(spawner) ?? bodyLoadout;
        if (settings == null
            || !_prototypes.TryIndex(settings.Role, out var prototype)
            || !_preferences.TryGetCachedPreferences(player.UserId, out var preferences)
            || preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            return;
        }

        var loadout = profile.Loadouts.TryGetValue(settings.Role, out var saved)
            ? saved.Clone()
            : new RoleLoadout(settings.Role);
        loadout.SetDefault(profile, player, _prototypes);
        loadout.EnsureValid(profile, player, IoCManager.Instance!);

        bodyLoadout ??= EnsureComp<GhostRoleLoadoutComponent>(mob);
        bodyLoadout.Role = settings.Role;
        bodyLoadout.Applied = true;

        if (loadout.SelectedLoadouts.Values.All(selected => selected.Count == 0))
            return;

        _spawning.EquipRoleLoadout(mob, loadout, prototype);
        _loadouts.GearEquipped(mob);
    }
}
