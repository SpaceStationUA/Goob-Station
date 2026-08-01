// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Humanoid;
using Content.Shared._Pirate.SlimeMorph;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.SlimeMorph;

/// <summary>
/// Lets a slimeperson freely re-customize their own look (staged; committed on Apply) and mimic
/// humanoids they have studied, recoloring the copied features toward their own slime skin.
/// </summary>
public sealed class SlimeMorphSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly MarkingManager _markings = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeMorphComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlimeMorphComponent, OpenSlimeMorphUiEvent>(OnOpenUi);
        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        Subs.BuiEvents<SlimeMorphComponent>(SlimeMorphUiKey.Key, subs =>
        {
            subs.Event<SlimeMorphSelectMarkingMessage>(OnSelectMarking);
            subs.Event<SlimeMorphChangeColorMessage>(OnChangeColor);
            subs.Event<SlimeMorphAddSlotMessage>(OnAddSlot);
            subs.Event<SlimeMorphRemoveSlotMessage>(OnRemoveSlot);
            subs.Event<SlimeMorphSetSkinColorMessage>(OnSetSkinColor);
            subs.Event<SlimeMorphSetEyeColorMessage>(OnSetEyeColor);
            subs.Event<SlimeMorphSetSexMessage>(OnSetSex);
            subs.Event<SlimeMorphSetGenderMessage>(OnSetGender);
            subs.Event<SlimeMorphSetHeightMessage>(OnSetHeight);
            subs.Event<SlimeMorphSetWidthMessage>(OnSetWidth);
            subs.Event<SlimeMorphApplyMessage>(OnApply);
            subs.Event<SlimeMorphResetMessage>(OnReset);
            subs.Event<SlimeMorphSelectTargetMessage>(OnSelectTarget);
            subs.Event<SlimeMorphMimicMessage>(OnMimic);
            subs.Event<SlimeMorphForgetMessage>(OnForget);
            subs.Event<SlimeMorphRevertMessage>(OnRevert);
        });
    }

    private void OnMapInit(Entity<SlimeMorphComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.MorphActionEntity, ent.Comp.MorphAction);
    }

    private void OnOpenUi(Entity<SlimeMorphComponent> ent, ref OpenSlimeMorphUiEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
        {
            ent.Comp.Opened = Capture(humanoid);
            ent.Comp.Staged = Capture(humanoid);
        }

        if (_ui.TryOpenUi(ent.Owner, SlimeMorphUiKey.Key, ent.Owner))
            UpdateUi(ent);

        args.Handled = true;
    }

    private static SlimeMorphWorking Capture(HumanoidAppearanceComponent humanoid)
    {
        return new SlimeMorphWorking
        {
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            Height = humanoid.Height,
            Width = humanoid.Width,
            Markings = new MarkingSet(humanoid.MarkingSet),
            HeadLayer = humanoid.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.Head, out var head)
                ? head.Id?.Id
                : null,
        };
    }

    private static SlimeMorphWorking Clone(SlimeMorphWorking working)
    {
        return new SlimeMorphWorking
        {
            Sex = working.Sex,
            Gender = working.Gender,
            SkinColor = working.SkinColor,
            EyeColor = working.EyeColor,
            Height = working.Height,
            Width = working.Width,
            Markings = new MarkingSet(working.Markings),
            HeadLayer = working.HeadLayer,
            PickerSpecies = working.PickerSpecies,
            FromTarget = working.FromTarget,
            SelectedTarget = working.SelectedTarget,
        };
    }

    /// <summary>
    /// The species' Head base-sprite id, but only if it's one we copy on mimic (a baked muzzle/nose).
    /// Returns null for races whose head we leave as the slime's own (identity carried by markings).
    /// </summary>
    private string? GetHeadLayer(SlimeMorphComponent comp, string speciesId, Sex sex)
    {
        if (!_proto.TryIndex<SpeciesPrototype>(speciesId, out var species)
            || !_proto.TryIndex<HumanoidSpeciesBaseSpritesPrototype>(species.SpriteSet, out var sprites)
            || !sprites.Sprites.TryGetValue(HumanoidVisualLayers.Head, out var headId))
            return null;

        return comp.HeadColorFactors.ContainsKey(headId)
            ? HumanoidVisualLayersExtension.GetSexMorph(HumanoidVisualLayers.Head, sex, headId)
            : null;
    }

    /// <summary>Brightness multiplier for a copied head so it matches the slime body's luminance.</summary>
    private static float HeadFactor(SlimeMorphComponent comp, string? headLayer)
    {
        if (headLayer == null)
            return 1f;

        foreach (var (baseId, factor) in comp.HeadColorFactors)
        {
            if (MatchesHeadBase(headLayer, baseId))
                return factor;
        }

        return 1f;
    }

    private static bool MatchesHeadBase(string headLayer, string baseId)
    {
        return headLayer == baseId
            || headLayer == HumanoidVisualLayersExtension.GetSexMorph(HumanoidVisualLayers.Head, Sex.Male, baseId)
            || headLayer == HumanoidVisualLayersExtension.GetSexMorph(HumanoidVisualLayers.Head, Sex.Female, baseId);
    }

    // ---- Study Appearance verb ----

    private void OnGetVerbs(Entity<HumanoidAppearanceComponent> target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (user == target.Owner || !TryComp<SlimeMorphComponent>(user, out var morph))
            return;

        if (!morph.MorphableSpecies.Contains(target.Comp.Species))
            return;

        var concealed = IsConcealed(target.Owner);
        var targetOwner = target.Owner;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("slime-morph-verb-study"),
            Priority = 1,
            Disabled = concealed,
            Message = concealed ? Loc.GetString("slime-morph-study-concealed") : null,
            Act = () => StudyAppearance((user, morph), targetOwner),
        });
    }

    private void StudyAppearance(Entity<SlimeMorphComponent> user, EntityUid target)
    {
        if (IsConcealed(target) || !TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
        {
            _popup.PopupEntity(Loc.GetString("slime-morph-study-concealed"), user, user);
            return;
        }

        var netTarget = GetNetEntity(target);
        var appearance = new SlimeMorphAppearance
        {
            Target = netTarget,
            Name = Identity.Name(target, EntityManager, user),
            Species = humanoid.Species,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            Height = humanoid.Height,
            Width = humanoid.Width,
            Markings = humanoid.MarkingSet.GetForwardEnumerator().ToList(),
            HeadLayer = GetHeadLayer(user.Comp, humanoid.Species, humanoid.Sex),
        };

        var refreshed = user.Comp.Remembered.ContainsKey(netTarget);
        user.Comp.Remembered[netTarget] = appearance;

        _popup.PopupEntity(
            Loc.GetString(refreshed ? "slime-morph-study-refreshed" : "slime-morph-study-success", ("name", appearance.Name)),
            user,
            user);

        if (_ui.IsUiOpen(user.Owner, SlimeMorphUiKey.Key))
            UpdateUi(user);
    }

    // ---- Target selection / preview (no cost) ----

    private void OnSelectTarget(Entity<SlimeMorphComponent> ent, ref SlimeMorphSelectTargetMessage args)
    {
        if (ent.Comp.Staged == null || !TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        // Deselect -> back to the self look.
        if (args.Target is not { } netTarget
            || !ent.Comp.Remembered.TryGetValue(netTarget, out var appearance))
        {
            ent.Comp.Staged = ent.Comp.Opened != null ? Clone(ent.Comp.Opened) : Capture(humanoid);
            UpdateUi(ent);
            return;
        }

        // Build the would-be mimic look: slime keeps its own skin; copied features are tinted toward it.
        var slimeSkin = humanoid.SkinColor;
        var staged = new SlimeMorphWorking
        {
            Sex = appearance.Sex,
            Gender = appearance.Gender,
            SkinColor = slimeSkin,
            EyeColor = Tint(appearance.EyeColor, slimeSkin, ent.Comp.TintFactor, 1f),
            Height = appearance.Height,
            Width = appearance.Width,
            Markings = new MarkingSet(),
            HeadLayer = appearance.HeadLayer,
            PickerSpecies = appearance.Species,
            FromTarget = true,
            SelectedTarget = netTarget,
        };

        foreach (var marking in appearance.Markings)
        {
            var tinted = new List<Color>(marking.MarkingColors.Count);
            foreach (var color in marking.MarkingColors)
                tinted.Add(Tint(color, slimeSkin, ent.Comp.TintFactor, ent.Comp.TintAlpha));

            AddForcedMarking(staged.Markings, marking.MarkingId, tinted);
        }

        ent.Comp.Staged = staged;
        UpdateUi(ent);
    }

    // ---- Commit (mimic = costs, apply = free) ----

    private void OnMimic(Entity<SlimeMorphComponent> ent, ref SlimeMorphMimicMessage args)
    {
        if (ent.Comp.Staged is not { FromTarget: true } staged
            || !TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        if (TryComp<HungerComponent>(ent.Owner, out var hunger)
            && _hunger.IsHungerBelowState(ent.Owner, HungerThreshold.Okay, null, hunger))
        {
            _popup.PopupEntity(Loc.GetString("slime-morph-mimic-hungry"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        // Remember our own look the first time, so "Revert" can always restore it.
        ent.Comp.OriginalAppearance ??= SnapshotSelf(humanoid);

        CommitStaged(
            ent.Owner,
            humanoid,
            staged,
            HeadFactor(ent.Comp, staged.HeadLayer),
            ent.Comp.HeadColorAlpha);
        SpendNutrition(ent.Owner, ent.Comp, hunger);
        Squish(ent.Owner, ent.Comp);
        Rebase(ent, humanoid);

        _popup.PopupEntity(Loc.GetString("slime-morph-mimic-success"), ent.Owner, ent.Owner);
        UpdateUi(ent);
    }

    private void OnApply(Entity<SlimeMorphComponent> ent, ref SlimeMorphApplyMessage args)
    {
        if (ent.Comp.Staged is not { FromTarget: false } staged
            || !TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        // Reshaping yourself is still morphing: gated by hunger, costs nutrition, squishes.
        var hunger = CompOrNull<HungerComponent>(ent.Owner);
        if (hunger != null && _hunger.IsHungerBelowState(ent.Owner, HungerThreshold.Okay, null, hunger))
        {
            _popup.PopupEntity(Loc.GetString("slime-morph-mimic-hungry"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        ent.Comp.OriginalAppearance ??= SnapshotSelf(humanoid);

        CommitStaged(
            ent.Owner,
            humanoid,
            staged,
            HeadFactor(ent.Comp, staged.HeadLayer),
            ent.Comp.HeadColorAlpha);
        SpendNutrition(ent.Owner, ent.Comp, hunger);
        Squish(ent.Owner, ent.Comp);
        Rebase(ent, humanoid);

        _popup.PopupEntity(Loc.GetString("slime-morph-apply-success"), ent.Owner, ent.Owner);
        UpdateUi(ent);
    }

    private void OnReset(Entity<SlimeMorphComponent> ent, ref SlimeMorphResetMessage args)
    {
        if (ent.Comp.Opened is not { } opened)
            return;

        ent.Comp.Staged = Clone(opened);
        UpdateUi(ent);
    }

    private void OnRevert(Entity<SlimeMorphComponent> ent, ref SlimeMorphRevertMessage args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        var target = ent.Comp.OriginalAppearance is { } original
            ? ToWorking(original)
            : ent.Comp.Opened;
        if (target == null)
            return;

        CommitStaged(
            ent.Owner,
            humanoid,
            target,
            HeadFactor(ent.Comp, target.HeadLayer),
            ent.Comp.HeadColorAlpha);
        SpendNutrition(ent.Owner, ent.Comp, CompOrNull<HungerComponent>(ent.Owner));
        Squish(ent.Owner, ent.Comp);
        Rebase(ent, humanoid);

        _popup.PopupEntity(Loc.GetString("slime-morph-revert-success"), ent.Owner, ent.Owner);
        UpdateUi(ent);
    }

    private void OnForget(Entity<SlimeMorphComponent> ent, ref SlimeMorphForgetMessage args)
    {
        if (!ent.Comp.Remembered.Remove(args.Target))
            return;

        // If we were previewing this target, drop back to the self look.
        if (ent.Comp.Staged is { } staged && staged.SelectedTarget == args.Target)
            ent.Comp.Staged = ent.Comp.Opened != null ? Clone(ent.Comp.Opened) : staged;

        UpdateUi(ent);
    }

    // ---- Self-customization (staged) ----

    private void OnSelectMarking(Entity<SlimeMorphComponent> ent, ref SlimeMorphSelectMarkingMessage args)
    {
        if (!IsSelfEditable(args.Category)
            || ent.Comp.Staged is not { } staged
            || !_markings.MarkingsByCategory(args.Category).TryGetValue(args.MarkingId, out var proto)
            || !staged.Markings.TryGetCategory(args.Category, out var list)
            || args.Slot < 0 || args.Slot >= list.Count)
            return;

        var marking = proto.AsMarking();
        for (var i = 0; i < marking.MarkingColors.Count && i < list[args.Slot].MarkingColors.Count; i++)
            marking.SetColor(i, list[args.Slot].MarkingColors[i]);

        // Preserve the slot's forced status.
        marking.Forced = list[args.Slot].Forced;
        staged.Markings.Replace(args.Category, args.Slot, marking);
    }

    private void OnChangeColor(Entity<SlimeMorphComponent> ent, ref SlimeMorphChangeColorMessage args)
    {
        if (!IsSelfEditable(args.Category)
            || ent.Comp.Staged is not { } staged
            || !staged.Markings.TryGetCategory(args.Category, out var list)
            || args.Slot < 0 || args.Slot >= list.Count)
            return;

        for (var i = 0; i < list[args.Slot].MarkingColors.Count && i < args.Colors.Count; i++)
            list[args.Slot].SetColor(i, args.Colors[i]);
    }

    private void OnAddSlot(Entity<SlimeMorphComponent> ent, ref SlimeMorphAddSlotMessage args)
    {
        if (!IsSelfEditable(args.Category)
            || ent.Comp.Staged is not { } staged
            || !TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        // Fall back to an all-species marking when the category is absent from the species.
        var pickerSpecies = staged.PickerSpecies ?? humanoid.Species;
        var markingId = _markings.MarkingsByCategoryAndSpecies(args.Category, pickerSpecies).Keys.FirstOrDefault()
            ?? _markings.MarkingsByCategory(args.Category).Keys.FirstOrDefault();
        if (string.IsNullOrEmpty(markingId) || !_markings.Markings.TryGetValue(markingId, out var proto))
            return;

        var marking = proto.AsMarking();
        // Allow cross-species selections.
        marking.Forced = true;
        staged.Markings.AddBack(args.Category, marking);
        UpdateUi(ent);
    }

    private void OnRemoveSlot(Entity<SlimeMorphComponent> ent, ref SlimeMorphRemoveSlotMessage args)
    {
        if (!IsSelfEditable(args.Category)
            || ent.Comp.Staged is not { } staged
            || !staged.Markings.TryGetCategory(args.Category, out var list)
            || args.Slot < 0 || args.Slot >= list.Count)
            return;

        staged.Markings.Remove(args.Category, args.Slot);
        UpdateUi(ent);
    }

    private void OnSetSkinColor(Entity<SlimeMorphComponent> ent, ref SlimeMorphSetSkinColorMessage args)
    {
        if (ent.Comp.Staged is { } staged)
            staged.SkinColor = args.Color;
    }

    private void OnSetEyeColor(Entity<SlimeMorphComponent> ent, ref SlimeMorphSetEyeColorMessage args)
    {
        if (ent.Comp.Staged is { } staged)
            staged.EyeColor = args.Color;
    }

    private void OnSetSex(Entity<SlimeMorphComponent> ent, ref SlimeMorphSetSexMessage args)
    {
        if (ent.Comp.Staged is { } staged)
            staged.Sex = args.Sex;
    }

    private void OnSetGender(Entity<SlimeMorphComponent> ent, ref SlimeMorphSetGenderMessage args)
    {
        if (ent.Comp.Staged is { } staged)
            staged.Gender = args.Gender;
    }

    private void OnSetHeight(Entity<SlimeMorphComponent> ent, ref SlimeMorphSetHeightMessage args)
    {
        if (ent.Comp.Staged is not { } staged || !TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        var species = _proto.Index<SpeciesPrototype>(humanoid.Species);
        staged.Height = Math.Clamp(args.Height, species.MinHeight, species.MaxHeight);
    }

    private void OnSetWidth(Entity<SlimeMorphComponent> ent, ref SlimeMorphSetWidthMessage args)
    {
        if (ent.Comp.Staged is not { } staged || !TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        var species = _proto.Index<SpeciesPrototype>(humanoid.Species);
        staged.Width = Math.Clamp(args.Width, species.MinWidth, species.MaxWidth);
    }

    private static bool IsSelfEditable(MarkingCategories category)
    {
        return Array.IndexOf(SlimeMorphCategories.Editable, category) >= 0;
    }

    // ---- Body commit helpers ----

    private void CommitStaged(
        EntityUid uid,
        HumanoidAppearanceComponent humanoid,
        SlimeMorphWorking staged,
        float factor,
        float alpha)
    {
        humanoid.MarkingSet = new MarkingSet(staged.Markings);
        _humanoid.SetSex(uid, staged.Sex, false, humanoid);
        _humanoid.SetGender(uid, staged.Gender, false, humanoid);
        _humanoid.SetSkinColor(uid, staged.SkinColor, false, humanoid: humanoid);
        humanoid.EyeColor = staged.EyeColor;
        _humanoid.SetScale(uid, new Vector2(staged.Width, staged.Height), false, humanoid);

        // SetSkinColor does not update custom body-part layers.
        foreach (var layer in humanoid.CustomBaseLayers.Keys.ToList())
        {
            if (layer is HumanoidVisualLayers.Eyes or HumanoidVisualLayers.Head)
                continue;

            _humanoid.SetBaseLayerColor(uid, layer, staged.SkinColor, false, humanoid);
        }

        // Apply the copied baked head sprite.
        if (staged.HeadLayer is { } headLayer)
        {
            _humanoid.SetBaseLayerId(uid, HumanoidVisualLayers.Head, headLayer, false, humanoid);
            _humanoid.SetBaseLayerColor(
                uid,
                HumanoidVisualLayers.Head,
                Darken(staged.SkinColor, factor).WithAlpha(alpha),
                false,
                humanoid);
        }
        else
        {
            humanoid.CustomBaseLayers.Remove(HumanoidVisualLayers.Head);
        }

        Dirty(uid, humanoid);
    }

    /// <summary>After a body-changing commit, rebase the menu buffers onto the new body (self look).</summary>
    private static void Rebase(Entity<SlimeMorphComponent> ent, HumanoidAppearanceComponent humanoid)
    {
        if (ent.Comp.Staged == null)
            return;

        ent.Comp.Staged = Capture(humanoid);
        ent.Comp.Opened = Capture(humanoid);
    }

    private SlimeMorphAppearance SnapshotSelf(HumanoidAppearanceComponent humanoid)
    {
        return new SlimeMorphAppearance
        {
            Species = humanoid.Species,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            Height = humanoid.Height,
            Width = humanoid.Width,
            Markings = humanoid.MarkingSet.GetForwardEnumerator().ToList(),
            HeadLayer = humanoid.CustomBaseLayers.TryGetValue(HumanoidVisualLayers.Head, out var head)
                ? head.Id?.Id
                : null,
        };
    }

    private SlimeMorphWorking ToWorking(SlimeMorphAppearance appearance)
    {
        var working = new SlimeMorphWorking
        {
            Sex = appearance.Sex,
            Gender = appearance.Gender,
            SkinColor = appearance.SkinColor,
            EyeColor = appearance.EyeColor,
            Height = appearance.Height,
            Width = appearance.Width,
            Markings = new MarkingSet(),
            HeadLayer = appearance.HeadLayer,
        };

        foreach (var marking in appearance.Markings)
            AddForcedMarking(working.Markings, marking.MarkingId, marking.MarkingColors);

        return working;
    }

    private void AddForcedMarking(MarkingSet set, string markingId, IReadOnlyList<Color> colors)
    {
        if (!_markings.Markings.TryGetValue(markingId, out var proto))
            return;

        var marking = new Marking(markingId, colors) { Forced = true };
        set.AddBack(proto.MarkingCategory, marking);
    }

    private void SpendNutrition(EntityUid uid, SlimeMorphComponent comp, HungerComponent? hunger)
    {
        if (hunger == null)
            return;

        var cost = _hunger.GetHunger(hunger) * comp.NutritionCostFraction;
        _hunger.ModifyHunger(uid, -cost, hunger);
    }

    private void Squish(EntityUid uid, SlimeMorphComponent comp)
    {
        _audio.PlayPvs(comp.MorphSound, uid);
    }

    // ---- UI state ----

    private void UpdateUi(Entity<SlimeMorphComponent> ent)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            return;

        var staged = ent.Comp.Staged;
        var species = _proto.Index<SpeciesPrototype>(humanoid.Species);

        var state = new SlimeMorphUiState
        {
            Species = humanoid.Species,
            Sex = staged?.Sex ?? humanoid.Sex,
            Gender = staged?.Gender ?? humanoid.Gender,
            SkinColor = staged?.SkinColor ?? humanoid.SkinColor,
            EyeColor = staged?.EyeColor ?? humanoid.EyeColor,
            Height = staged?.Height ?? humanoid.Height,
            Width = staged?.Width ?? humanoid.Width,
            MinHeight = species.MinHeight,
            MaxHeight = species.MaxHeight,
            MinWidth = species.MinWidth,
            MaxWidth = species.MaxWidth,
            MarkingSet = staged?.Markings ?? humanoid.MarkingSet,
            PickerSpecies = staged?.PickerSpecies,
            HeadLayer = staged?.HeadLayer,
            HeadColorFactor = HeadFactor(ent.Comp, staged?.HeadLayer),
            HeadColorAlpha = ent.Comp.HeadColorAlpha,
            Remembered = ent.Comp.Remembered.Values.ToList(),
            SelectedTarget = staged?.SelectedTarget,
            CanApply = staged is { FromTarget: false },
            CanMimic = staged is { FromTarget: true },
        };

        _ui.SetUiState(ent.Owner, SlimeMorphUiKey.Key, state);
    }

    /// <summary>Blend a copied color toward the slime's skin, then apply translucency.</summary>
    private static Color Tint(Color original, Color slimeSkin, float factor, float alpha)
    {
        return Color.InterpolateBetween(original, slimeSkin, factor).WithAlpha(alpha);
    }

    /// <summary>Scale a color's brightness (RGB) to tone a copied head down to slime-body luminance.</summary>
    private static Color Darken(Color color, float factor)
    {
        return new Color(color.R * factor, color.G * factor, color.B * factor, color.A);
    }

    /// <summary>Returns whether the target's identity is hidden.</summary>
    private bool IsConcealed(EntityUid target)
    {
        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(target, ev);
        return ev.Cancelled;
    }
}
