// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializePirateKnowledgeEditor()
    {
        KnowledgeEditor.OnApply += profile =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithKnowledge(profile);
            SetDirty();
        };
    }

    private void UpdatePirateKnowledgeEditor()
    {
        if (Profile is not null)
            KnowledgeEditor.SetProfile(Profile.Species, Profile.Knowledge);
    }
}
