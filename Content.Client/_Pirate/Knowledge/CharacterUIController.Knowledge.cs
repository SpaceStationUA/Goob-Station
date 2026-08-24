// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client.UserInterface.Systems.Character;

public sealed partial class CharacterUIController
{
    private EntityUid? _pirateKnowledgeEntity;

    private void UpdatePirateKnowledgeTab(EntityUid entity)
    {
        _pirateKnowledgeEntity = entity;
        _window?.KnowledgeTabControl.UpdateKnowledgeTab(entity);
    }

    private void OnPirateKnowledgeChanged()
    {
        if (_pirateKnowledgeEntity is { } entity)
            _window?.KnowledgeTabControl.UpdateKnowledgeTab(entity);
    }
}
