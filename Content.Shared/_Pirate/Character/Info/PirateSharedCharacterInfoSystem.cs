// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Character.Info.Components;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Trigger.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Character.Info;

/// <summary>
/// Applies and exposes extended IC and OOC character information.
/// </summary>
public abstract partial class PirateSharedCharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    private bool _characterWindowEnabled;
    private bool _flavorTextEnabled;
    private bool _exploitableSecretsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _config.OnValueChanged(CCVars.FlavorText, value => _flavorTextEnabled = value, true);
        _config.OnValueChanged(PirateVars.ExploitableSecrets, value => _exploitableSecretsEnabled = value, true);
        _config.OnValueChanged(PirateVars.CharacterInspectWindowEnabled, value => _characterWindowEnabled = value, true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<HumanoidAppearanceComponent, DnaScrambledEvent>(OnDnaScrambled);
        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<ExamineVerb>>(OnExamineCharacter);
        SubscribeLocalEvent<ExploitableInfoComponent, GetVerbsEvent<ExamineVerb>>(OnExamineExploitableInfo);

        SubscribeLocalEvent<CharacterSecretsComponent, ComponentGetStateAttemptEvent>(OnCharacterSecretsStateAttempt);
        SubscribeLocalEvent<ExploitableInfoComponent, ComponentGetStateAttemptEvent>(OnExploitableStateAttempt);
        SubscribeLocalEvent<MindSecretsComponent, ComponentGetStateAttemptEvent>(OnMindSecretsStateAttempt);

        SubscribeLocalEvent<RoleAddedEvent>(OnRoleChanged);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleChanged);
        SubscribeLocalEvent<GhostComponent, ComponentStartup>(OnGhostStartup);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        ApplyCharacterInfo(ev.Mob, ev.Profile);
    }

    public void ApplyCharacterInfo(EntityUid mob, HumanoidCharacterProfile profile)
    {
        var mind = _minds.GetMind(mob);
        if (mind != null)
        {
            if (_config.GetCVar(CCVars.FlavorText))
            {
                AddComp(mind.Value, new CharacterDescriptionComponent
                {
                    Description = profile.PersonalityDescription,
                }, true);
            }

            if (_config.GetCVar(PirateVars.OOCNotes))
            {
                AddComp(mind.Value, new RoleplayInfoComponent
                {
                    OOCNotes = profile.OOCNotes,
                }, true);

                AddComp(mind.Value, new MindSecretsComponent
                {
                    PersonalNotes = profile.PersonalNotes,
                }, true);
            }
        }

        if (_config.GetCVar(CCVars.FlavorText))
        {
            AddComp(mob, new CharacterDescriptionComponent
            {
                Description = profile.PhysicalDescription,
            }, true);
        }

        if (_config.GetCVar(PirateVars.ExploitableSecrets))
        {
            AddComp(mob, new ExploitableInfoComponent
            {
                Info = profile.ExploitableInfo,
            }, true);
        }

        if (_config.GetCVar(PirateVars.ICSecrets))
        {
            AddComp(mob, new CharacterSecretsComponent
            {
                Secrets = profile.Secrets,
            }, true);
        }
    }

    private void OnDnaScrambled(Entity<HumanoidAppearanceComponent> entity, ref DnaScrambledEvent args)
    {
        if (TryComp(entity, out ExploitableInfoComponent? exploitable))
        {
            exploitable.Info = string.Empty;
            Dirty(entity, exploitable);
        }

        if (TryComp(entity, out CharacterDescriptionComponent? physicalDescription))
        {
            physicalDescription.Description = string.Empty;
            Dirty(entity, physicalDescription);
        }

        var mind = _minds.GetMind(entity);
        if (mind != null && TryComp(mind.Value, out CharacterDescriptionComponent? personalityDescription))
        {
            personalityDescription.Description = string.Empty;
            Dirty(mind.Value, personalityDescription);
        }
    }

    private void OnCharacterSecretsStateAttempt(
        Entity<CharacterSecretsComponent> entity,
        ref ComponentGetStateAttemptEvent args)
    {
        if (args.Cancelled || args.Player == null)
            return;

        args.Cancelled = !CanAccessSecretData(entity, args.Player.AttachedEntity);
    }

    private void OnExploitableStateAttempt(
        Entity<ExploitableInfoComponent> entity,
        ref ComponentGetStateAttemptEvent args)
    {
        if (args.Cancelled || args.Player == null)
            return;

        args.Cancelled = !CanAccessExploitableData(entity, args.Player.AttachedEntity);
    }

    private void OnMindSecretsStateAttempt(
        Entity<MindSecretsComponent> entity,
        ref ComponentGetStateAttemptEvent args)
    {
        if (args.Cancelled || args.Player == null)
            return;

        args.Cancelled = args.Player.GetMind() != entity.Owner;
    }

    private void OnRoleChanged(RoleAddedEvent args)
    {
        DirtyExploitableInfo();
    }

    private void OnRoleChanged(RoleRemovedEvent args)
    {
        DirtyExploitableInfo();
    }

    private void OnGhostStartup(Entity<GhostComponent> entity, ref ComponentStartup args)
    {
        DirtyExploitableInfo();
    }

    private void DirtyExploitableInfo()
    {
        if (_net.IsClient)
            return;

        var query = AllEntityQuery<ExploitableInfoComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            Dirty(uid, component);
        }
    }

    private void OnExamineCharacter(Entity<HumanoidAppearanceComponent> entity, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
            return;

        var detailsRange = _examine.IsInDetailsRange(args.User, entity);

        if (_characterWindowEnabled)
        {
            args.Verbs.Add(new ExamineVerb
            {
                Act = () => OpenCharacterWindow(entity, args.User),
                Disabled = !detailsRange,
                Message = detailsRange ? null : Loc.GetString("detail-examine-verb-disabled"),
                Text = Loc.GetString("character-info-inspect-prompt"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/_Pirate/Interface/VerbIcons/examine-character-menu.png")),
            });
        }

        if (!_flavorTextEnabled)
            return;

        var description = GetPublicDescription(entity);
        if (string.IsNullOrEmpty(description))
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Act = () =>
            {
                var markup = new FormattedMessage();
                markup.AddMarkupPermissive(description);
                _examine.SendExamineTooltip(args.User, entity, markup, false, false);
            },
            Text = Loc.GetString("detail-examine-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detail-examine-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),
        });
    }

    private void OnExamineExploitableInfo(Entity<ExploitableInfoComponent> entity, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName
            || !_exploitableSecretsEnabled
            || string.IsNullOrEmpty(entity.Comp.Info)
            || !CanAccessExploitableData(entity, args.User))
        {
            return;
        }

        var detailsRange = _examine.IsInDetailsRange(args.User, entity);
        args.Verbs.Add(new ExamineVerb
        {
            Act = () =>
            {
                var markup = new FormattedMessage();
                markup.AddMarkupPermissive(entity.Comp.Info);
                _examine.SendExamineTooltip(args.User, entity, markup, false, false);
            },
            Text = Loc.GetString("exploitable-examine-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detail-examine-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/_Pirate/Interface/VerbIcons/examine-exploitable.png")),
        });
    }

    public string GetPublicDescription(EntityUid target)
    {
        var descriptions = new List<string>(2);

        if (TryComp(target, out CharacterDescriptionComponent? physical)
            && !string.IsNullOrEmpty(physical.Description))
        {
            descriptions.Add(physical.Description);
        }

        var mind = _minds.GetMind(target);
        if (mind != null
            && TryComp(mind.Value, out CharacterDescriptionComponent? personality)
            && !string.IsNullOrEmpty(personality.Description))
        {
            descriptions.Add(personality.Description);
        }

        return string.Join("\n", descriptions);
    }

    public string? GetPhysicalDescription(EntityUid target)
    {
        return TryComp(target, out CharacterDescriptionComponent? description)
            ? description.Description
            : null;
    }

    public void SetPhysicalDescription(EntityUid target, string description)
    {
        var component = EnsureComp<CharacterDescriptionComponent>(target);
        component.Description = description;
        Dirty(target, component);
    }

    public bool CanAccessExploitableData(EntityUid target, EntityUid? requester)
    {
        if (requester == null)
            return false;

        if (target == requester || HasComp<GhostComponent>(requester.Value))
            return true;

        return _roles.MindIsAntagonist(_minds.GetMind(requester.Value));
    }

    public bool CanAccessSecretData(EntityUid target, EntityUid? requester)
    {
        return target == requester;
    }

    protected virtual void OpenCharacterWindow(EntityUid target, EntityUid requester)
    {
    }
}
