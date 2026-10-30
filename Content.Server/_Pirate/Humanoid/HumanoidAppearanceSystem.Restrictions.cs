using System.Threading.Tasks;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Server.Preferences.Managers;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Humanoid;

public sealed partial class HumanoidAppearanceSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;

    private void OnAppearanceStartup(EntityUid uid, HumanoidAppearanceComponent component, ComponentStartup args)
    {
        SanitizeAppearance(uid, component);
    }

    public void SanitizeAppearance(EntityUid uid, HumanoidAppearanceComponent humanoid, string? ckey = null)
    {
        ckey ??= GetOwnerCkey(uid);
        if (_markingManager.FilterPlayerRestrictedMarkings(humanoid.MarkingSet, ckey))
            Dirty(uid, humanoid);
    }

    private HumanoidCharacterProfile SanitizeProfile(EntityUid uid, HumanoidCharacterProfile profile, string? ckey = null)
    {
        ckey ??= GetOwnerCkey(uid);
        var appearance = new HumanoidCharacterAppearance(profile.Appearance)
        {
            Markings = _markingManager.FilterValidMarkings(profile.Appearance.Markings, profile.Species, profile.Sex, ckey),
        };

        if (!_markingManager.Markings.TryGetValue(appearance.HairStyleId, out var hair) ||
            !_markingManager.CanBeApplied(profile.Species, profile.Sex, hair, ckey: ckey))
            appearance.HairStyleId = HairStyles.DefaultHairStyle;

        if (!_markingManager.Markings.TryGetValue(appearance.FacialHairStyleId, out var facialHair) ||
            !_markingManager.CanBeApplied(profile.Species, profile.Sex, facialHair, ckey: ckey))
            appearance.FacialHairStyleId = HairStyles.DefaultFacialHairStyle;

        return profile.WithCharacterAppearance(appearance);
    }

    public override void LoadProfile(EntityUid uid, HumanoidCharacterProfile? profile, HumanoidAppearanceComponent? humanoid = null)
    {
        LoadProfileForPlayer(uid, profile, null, humanoid);
    }

    public void LoadProfileForPlayer(EntityUid uid, HumanoidCharacterProfile? profile, string? ckey, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!string.IsNullOrWhiteSpace(ckey) && profile != null)
            _ = PersistRestrictedMarkingCleanup(profile, ckey);

        if (profile != null)
            profile = SanitizeProfile(uid, profile, ckey);

        base.LoadProfile(uid, profile, humanoid);

        if (profile == null || !Resolve(uid, ref humanoid))
            return;

        var changed = RestoreRestrictedHair(uid, humanoid, profile, MarkingCategories.Hair,
            profile.Appearance.HairStyleId, profile.Appearance.HairColor, HumanoidVisualLayers.Hair, ckey);
        changed |= RestoreRestrictedHair(uid, humanoid, profile, MarkingCategories.FacialHair,
            profile.Appearance.FacialHairStyleId, profile.Appearance.FacialHairColor, HumanoidVisualLayers.FacialHair, ckey);

        if (changed)
            Dirty(uid, humanoid);
    }

    private bool RestoreRestrictedHair(EntityUid uid, HumanoidAppearanceComponent humanoid,
        HumanoidCharacterProfile profile, MarkingCategories category, string markingId, Color color,
        HumanoidVisualLayers layer, string? ckey)
    {
        if (!_markingManager.Markings.TryGetValue(markingId, out var marking)
            || marking.PlayerRestrictions is not { Count: > 0 }
            || !_markingManager.CanBeApplied(profile.Species, profile.Sex, marking, ckey: ckey))
            return false;

        if (_markingManager.MustMatchSkin(profile.Species, layer, out var alpha))
            color = profile.Appearance.SkinColor.WithAlpha(alpha);

        humanoid.MarkingSet.RemoveCategory(category);
        AddMarking(uid, markingId, color, false, humanoid: humanoid);
        return true;
    }

    private async Task PersistRestrictedMarkingCleanup(HumanoidCharacterProfile loadedProfile, string ckey)
    {
        if (!_playerManager.TryGetSessionByUsername(ckey, out var session)
            || !_preferences.TryGetCachedPreferences(session.UserId, out var preferences)
            || preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        // Only persist the selected profile that was actually loaded, and only purge player restrictions.
        if (!profile.Equals(loadedProfile))
            return;

        var appearance = new HumanoidCharacterAppearance(profile.Appearance)
        {
            Markings = _markingManager.FilterPlayerRestrictedMarkings(profile.Appearance.Markings, ckey),
        };

        if (_markingManager.Markings.TryGetValue(appearance.HairStyleId, out var hair)
            && hair.PlayerRestrictions is { Count: > 0 }
            && !MarkingManager.CanPlayerUseMarking(hair, ckey))
            appearance.HairStyleId = HairStyles.DefaultHairStyle;

        if (_markingManager.Markings.TryGetValue(appearance.FacialHairStyleId, out var facialHair)
            && facialHair.PlayerRestrictions is { Count: > 0 }
            && !MarkingManager.CanPlayerUseMarking(facialHair, ckey))
            appearance.FacialHairStyleId = HairStyles.DefaultFacialHairStyle;

        var sanitized = profile.WithCharacterAppearance(appearance);
        if (!profile.Equals(sanitized))
        {
            try
            {
                await _preferences.SetProfile(session.UserId, preferences.SelectedCharacterIndex, sanitized);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to remove restricted markings from {ckey}'s character profile: {e}");
            }
        }
    }

    private string? GetOwnerCkey(EntityUid uid)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            return actor.PlayerSession.Name;

        if (!TryComp<MindContainerComponent>(uid, out var container)
            || container.Mind is not { } mindId
            || !TryComp<MindComponent>(mindId, out var mind)
            || mind.UserId is not { } userId
            || !_playerManager.TryGetPlayerData(userId, out var playerData))
            return null;

        return playerData.UserName;
    }

}
