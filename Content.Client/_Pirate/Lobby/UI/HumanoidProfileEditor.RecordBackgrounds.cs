// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void SetupRecordBackgroundEditor()
    {
        RecordBackgroundEditor.OnChanged += () =>
        {
            if (Profile is null || _suppressCharacterInfoChanges)
                return;

            Profile = Profile.WithRecordBackgrounds(RecordBackgroundEditor.GetEntries());
            SetDirty();
        };
    }

    private void UpdateRecordBackgroundText()
    {
        RecordBackgroundEditor.SetEntries(Profile?.RecordBackgrounds ?? []);
    }
}
