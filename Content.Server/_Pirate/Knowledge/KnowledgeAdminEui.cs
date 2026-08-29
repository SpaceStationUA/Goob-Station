// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Knowledge;

[UsedImplicitly]
public sealed class KnowledgeAdminEui : BaseEui
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly SharedKnowledgeSystem _knowledge;
    private readonly EntityUid _target;

    public KnowledgeAdminEui(EntityUid target)
    {
        IoCManager.InjectDependencies(this);
        _knowledge = _entities.System<SharedKnowledgeSystem>();
        _target = target;
    }

    public override void Opened()
    {
        base.Opened();
        _admins.OnPermsChanged += OnPermissionsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _admins.OnPermsChanged -= OnPermissionsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        if (!_entities.EntityExists(_target))
        {
            return new KnowledgeAdminEuiState(
                null,
                Loc.GetString("knowledge-admin-target-unavailable"),
                []);
        }

        var skills = new List<KnowledgeAdminEntry>(_knowledge.AllKnowledges.Count);
        foreach (var (id, prototypeComponent) in _knowledge.AllKnowledges)
        {
            var current = _knowledge.GetKnowledge(_target, id);
            var component = current?.Comp ?? prototypeComponent;
            var prototype = _prototypes.Index<EntityPrototype>(id);
            var category = _prototypes.Index<KnowledgeCategoryPrototype>(component.Category);

            skills.Add(new KnowledgeAdminEntry(
                id.Id,
                prototype.Name,
                prototype.Description,
                Loc.GetString(category.Name),
                component.Hidden,
                current is not null,
                current?.Comp.LearnedLevel ?? 0,
                current?.Comp.TemporaryLevel ?? 0,
                current?.Comp.Experience ?? 0,
                component.ExperienceCost));
        }

        return new KnowledgeAdminEuiState(
            _entities.GetNetEntity(_target),
            _entities.GetComponent<MetaDataComponent>(_target).EntityName,
            skills);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admins.HasAdminFlag(Player, AdminFlags.Debug) || !_entities.EntityExists(_target))
        {
            Close();
            return;
        }

        switch (msg)
        {
            case KnowledgeAdminEuiMsg.Refresh:
                StateDirty();
                return;
            case KnowledgeAdminEuiMsg.Apply apply:
                ApplyChanges(apply.Changes);
                return;
        }
    }

    private void ApplyChanges(Dictionary<string, KnowledgeAdminEdit> changes)
    {
        if (changes.Count == 0 || changes.Count > _knowledge.AllKnowledges.Count)
            return;

        var changed = 0;
        foreach (var (prototype, edit) in changes)
        {
            var id = new EntProtoId(prototype);
            if (!_knowledge.AllKnowledges.ContainsKey(id))
                continue;

            var previous = _knowledge.GetKnowledge(_target, id);
            var oldLevel = previous?.Comp.LearnedLevel ?? 0;
            var oldExperience = previous?.Comp.Experience ?? 0;

            if (_knowledge.SetKnowledgeProgress(_target, id, edit.LearnedLevel, edit.Experience) is not { } result)
                continue;

            if (oldLevel != result.Comp.LearnedLevel || oldExperience != result.Comp.Experience || previous is null)
                changed++;
        }

        if (changed > 0)
        {
            _adminLog.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{Player:actor} changed {changed} skill entries on {_entities.ToPrettyString(_target):subject}");
        }

        StateDirty();
    }

    private void OnPermissionsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Debug))
            Close();
    }
}
