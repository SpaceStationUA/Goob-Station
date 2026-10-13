// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void SetupRecordBackgroundEditor()
    {
        RecordBackgroundEditor.SecurityInput.OnTextChanged += args =>
            ChangeRecordBackground(args, (profile, text) => profile.WithSecurityRecordBackground(text));
        RecordBackgroundEditor.CrewInput.OnTextChanged += args =>
            ChangeRecordBackground(args, (profile, text) => profile.WithCrewRecordBackground(text));
        RecordBackgroundEditor.PsionicsInput.OnTextChanged += args =>
            ChangeRecordBackground(args, (profile, text) => profile.WithPsionicsRecordBackground(text));
    }

    private void UpdateRecordBackgroundText()
    {
        RecordBackgroundEditor.SecurityInput.TextRope = new Rope.Leaf(Profile?.SecurityRecordBackground ?? string.Empty);
        RecordBackgroundEditor.CrewInput.TextRope = new Rope.Leaf(Profile?.CrewRecordBackground ?? string.Empty);
        RecordBackgroundEditor.PsionicsInput.TextRope = new Rope.Leaf(Profile?.PsionicsRecordBackground ?? string.Empty);
        RecordBackgroundEditor.UpdateCounters();
    }

    private void ChangeRecordBackground(TextEdit.TextEditEventArgs args,
        Func<HumanoidCharacterProfile, string, HumanoidCharacterProfile> with)
    {
        if (Profile is null || _suppressCharacterInfoChanges)
            return;

        Profile = with(Profile, Rope.Collapse(args.TextRope).Trim());
        SetDirty();
    }
}
