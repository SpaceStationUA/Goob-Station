// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using Content.Client.Humanoid;
using Content.Client.UserInterface.Controls; // Pirate: slime morph - import name dialog
using Content.Shared._Pirate.SlimeMorph;
using Content.Shared.Administration; // Pirate: slime morph - QuickDialogEntry
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.SlimeMorph;

public sealed class SlimeMorphBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IFileDialogManager _dialogManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;

    [ViewVariables]
    private SlimeMorphWindow? _window;

    // Latest state, kept so Export can rebuild a profile from the current look without round-tripping.
    private SlimeMorphUiState? _lastState;

    public SlimeMorphBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SlimeMorphWindow>();

        _window.OnMarkingSelected += args =>
            SendMessage(new SlimeMorphSelectMarkingMessage(args.category, args.slot, args.id));
        _window.OnMarkingColorChanged += args =>
            SendMessage(new SlimeMorphChangeColorMessage(args.category, args.slot, new(args.marking.MarkingColors)));
        _window.OnMarkingSlotAdded += category =>
            SendMessage(new SlimeMorphAddSlotMessage(category));
        _window.OnMarkingSlotRemoved += args =>
            SendMessage(new SlimeMorphRemoveSlotMessage(args.category, args.slot));

        _window.OnSkinColorChanged += color => SendMessage(new SlimeMorphSetSkinColorMessage(color));
        _window.OnEyeColorChanged += color => SendMessage(new SlimeMorphSetEyeColorMessage(color));
        _window.OnSexChanged += sex => SendMessage(new SlimeMorphSetSexMessage(sex));
        _window.OnGenderChanged += gender => SendMessage(new SlimeMorphSetGenderMessage(gender));
        _window.OnHeightChanged += height => SendMessage(new SlimeMorphSetHeightMessage(height));
        _window.OnWidthChanged += width => SendMessage(new SlimeMorphSetWidthMessage(width));

        _window.OnSelectTarget += target => SendMessage(new SlimeMorphSelectTargetMessage(target));
        _window.OnMimic += () => SendMessage(new SlimeMorphMimicMessage());
        _window.OnForget += target => SendMessage(new SlimeMorphForgetMessage(target));
        _window.OnRevert += () => SendMessage(new SlimeMorphRevertMessage());
        _window.OnApply += () => SendMessage(new SlimeMorphApplyMessage());
        _window.OnReset += () => SendMessage(new SlimeMorphResetMessage());

        // Pirate: slime morph v1.1
        _window.OnXenotypeChanged += species => SendMessage(new SlimeMorphSetXenotypeMessage(species));
        _window.OnAdaptColors += () => SendMessage(new SlimeMorphAdaptColorsMessage());
        _window.OnSaveAppearance += name => SendMessage(new SlimeMorphSaveAppearanceMessage(name));
        _window.OnSelectSaved += args => SendMessage(new SlimeMorphSelectSavedMessage(args.name, args.species));
        _window.OnDeleteSaved += args => SendMessage(new SlimeMorphDeleteSavedMessage(args.name, args.species));
        _window.OnImport += Import;
        _window.OnExport += Export;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SlimeMorphUiState morphState)
            return;

        _lastState = morphState;
        _window?.UpdateState(morphState);
    }

    // ---- Import / export (character-editor .yml, interchangeable) ----

    private async void Import()
    {
        await using var file = await _dialogManager.OpenFile(
            new FileDialogFilters(new FileDialogFilters.Group("yml")), FileAccess.Read);
        if (file == null)
            return;

        SlimeMorphAppearance appearance;
        try
        {
            var profile = _entManager.System<HumanoidAppearanceSystem>().FromStream(file, _playerManager.LocalSession!);
            appearance = ProfileToAppearance(profile);
        }
        catch (Exception)
        {
            // Malformed / incompatible file - ignore, nothing gets applied.
            return;
        }

        // Ask for a name for the imported look. Left empty by default - the OS file name isn't
        // reachable from sandboxed content, and the in-file character name is worse than blank.
        var dialog = new DialogWindow(
            Loc.GetString("slime-morph-import-title"),
            new List<QuickDialogEntry>
            {
                new("name", QuickDialogEntryType.ShortText,
                    Loc.GetString("slime-morph-import-prompt"),
                    Loc.GetString("slime-morph-save-placeholder")),
            });

        dialog.OnConfirmed += results =>
        {
            var name = results.TryGetValue("name", out var entered) && !string.IsNullOrWhiteSpace(entered)
                ? entered.Trim()
                : string.Empty;

            appearance.Name = name;
            SendMessage(new SlimeMorphImportMessage(appearance));
            _window?.SetSuggestedName(name);
        };
    }

    private async void Export()
    {
        if (_lastState is not { } state)
            return;

        var file = await _dialogManager.SaveFile(new FileDialogFilters(new FileDialogFilters.Group("yml")));
        if (file == null)
            return;

        try
        {
            var dataNode = _entManager.System<HumanoidAppearanceSystem>().ToDataNode(BuildProfile(state));
            await using var writer = new StreamWriter(file.Value.fileStream);
            dataNode.Write(writer);
        }
        catch (Exception)
        {
            // Serialization failed - the file dialog stream is closed by the using above on success;
            // on an early throw it is left to GC, matching the character editor's export.
        }
    }

    /// <summary>
    /// Turn the current staged look into a full character profile. Hair / facial hair live in their
    /// own profile fields, so they are split out of the flat morph marking set.
    /// </summary>
    private HumanoidCharacterProfile BuildProfile(SlimeMorphUiState state)
    {
        var species = state.PickerSpecies ?? state.Species;

        string hairId = HairStyles.DefaultHairStyle;
        var hairColor = Color.Black;
        string facialId = HairStyles.DefaultFacialHairStyle;
        var facialColor = Color.Black;
        var rest = new List<Marking>();

        foreach (var marking in state.MarkingSet.GetForwardEnumerator())
        {
            if (!_markingManager.TryGetMarking(marking, out var proto))
                continue;

            switch (proto.MarkingCategory)
            {
                case MarkingCategories.Hair:
                    hairId = marking.MarkingId;
                    if (marking.MarkingColors.Count > 0)
                        hairColor = marking.MarkingColors[0];
                    break;
                case MarkingCategories.FacialHair:
                    facialId = marking.MarkingId;
                    if (marking.MarkingColors.Count > 0)
                        facialColor = marking.MarkingColors[0];
                    break;
                default:
                    rest.Add(new Marking(marking.MarkingId, marking.MarkingColors));
                    break;
            }
        }

        var appearance = new HumanoidCharacterAppearance(
            hairId, hairColor, facialId, facialColor, state.EyeColor, state.SkinColor, rest);

        return HumanoidCharacterProfile.DefaultWithSpecies(species)
            .WithSex(state.Sex)
            .WithGender(state.Gender)
            .WithHeight(state.Height)
            .WithWidth(state.Width)
            .WithCharacterAppearance(appearance);
    }

    /// <summary>
    /// Turn an imported profile into a morph appearance, re-injecting hair / facial hair as markings
    /// (the flat representation the morph editor uses).
    /// </summary>
    private SlimeMorphAppearance ProfileToAppearance(HumanoidCharacterProfile profile)
    {
        var app = profile.Appearance;
        var markings = new List<Marking>(app.Markings);

        AddHairMarking(markings, app.HairStyleId, app.HairColor);
        AddHairMarking(markings, app.FacialHairStyleId, app.FacialHairColor);

        return new SlimeMorphAppearance
        {
            Target = NetEntity.Invalid,
            Name = profile.Name,
            Species = profile.Species,
            Sex = profile.Sex,
            Gender = profile.Gender,
            SkinColor = app.SkinColor,
            EyeColor = app.EyeColor,
            Height = profile.Height,
            Width = profile.Width,
            Markings = markings,
        };
    }

    private void AddHairMarking(List<Marking> markings, string id, Color color)
    {
        if (!_markingManager.Markings.TryGetValue(id, out var proto))
            return;

        var count = Math.Max(1, proto.ColorCount);
        var colors = new List<Color>(count);
        for (var i = 0; i < count; i++)
            colors.Add(color);

        markings.Add(new Marking(id, colors));
    }
}
