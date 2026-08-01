// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Enums;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.SlimeMorph;

[Serializable, NetSerializable]
public enum SlimeMorphUiKey : byte
{
    Key
}

/// <summary>
/// A frozen snapshot of a humanoid's looks, sampled via the "Study Appearance" verb.
/// The slime keeps its own name/species; only these visual attributes are copied (tinted) on mimic.
/// </summary>
[Serializable, NetSerializable]
public sealed class SlimeMorphAppearance
{
    public NetEntity Target;
    public string Name = string.Empty;
    public string Species = string.Empty;
    public Sex Sex;
    public Gender Gender;
    public Color SkinColor;
    public Color EyeColor;
    public float Height = 1f;
    public float Width = 1f;
    public List<Marking> Markings = new();

    /// <summary>
    /// The target species' head base-sprite id (e.g. a fox muzzle), copied so baked head shapes
    /// transfer on mimic. Null = keep the slime's own head.
    /// </summary>
    public string? HeadLayer;
}

[Serializable, NetSerializable]
public sealed class SlimeMorphUiState : BoundUserInterfaceState
{
    public string Species = string.Empty;
    public Sex Sex;
    public Gender Gender;
    public Color SkinColor;
    public Color EyeColor;
    public float Height;
    public float Width;
    public float MinHeight;
    public float MaxHeight;
    public float MinWidth;
    public float MaxWidth;
    public MarkingSet MarkingSet = new();
    public List<SlimeMorphAppearance> Remembered = new();

    /// <summary>Head base-sprite override for the staged look (baked head shapes like muzzles).</summary>
    public string? HeadLayer;

    /// <summary>Brightness multiplier for the copied head so it matches the slime body.</summary>
    public float HeadColorFactor = 1f;

    /// <summary>Opacity multiplier for an opaque copied head so it matches translucent slime body art.</summary>
    public float HeadColorAlpha = 1f;

    /// <summary>Currently selected remembered target (the preview/mimic subject), if any.</summary>
    public NetEntity? SelectedTarget;

    /// <summary>Free self-edit commit is available (no target is loaded).</summary>
    public bool CanApply;

    /// <summary>A target is loaded and can be committed via mimic (costs nutrition).</summary>
    public bool CanMimic;
}

// ---- Self-customization messages (staged, free) ----

[Serializable, NetSerializable]
public sealed class SlimeMorphSelectMarkingMessage : BoundUserInterfaceMessage
{
    public MarkingCategories Category;
    public int Slot;
    public string MarkingId;

    public SlimeMorphSelectMarkingMessage(MarkingCategories category, int slot, string markingId)
    {
        Category = category;
        Slot = slot;
        MarkingId = markingId;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphChangeColorMessage : BoundUserInterfaceMessage
{
    public MarkingCategories Category;
    public int Slot;
    public List<Color> Colors;

    public SlimeMorphChangeColorMessage(MarkingCategories category, int slot, List<Color> colors)
    {
        Category = category;
        Slot = slot;
        Colors = colors;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphAddSlotMessage : BoundUserInterfaceMessage
{
    public MarkingCategories Category;

    public SlimeMorphAddSlotMessage(MarkingCategories category)
    {
        Category = category;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphRemoveSlotMessage : BoundUserInterfaceMessage
{
    public MarkingCategories Category;
    public int Slot;

    public SlimeMorphRemoveSlotMessage(MarkingCategories category, int slot)
    {
        Category = category;
        Slot = slot;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphSetSkinColorMessage : BoundUserInterfaceMessage
{
    public Color Color;

    public SlimeMorphSetSkinColorMessage(Color color)
    {
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphSetEyeColorMessage : BoundUserInterfaceMessage
{
    public Color Color;

    public SlimeMorphSetEyeColorMessage(Color color)
    {
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphSetSexMessage : BoundUserInterfaceMessage
{
    public Sex Sex;

    public SlimeMorphSetSexMessage(Sex sex)
    {
        Sex = sex;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphSetGenderMessage : BoundUserInterfaceMessage
{
    public Gender Gender;

    public SlimeMorphSetGenderMessage(Gender gender)
    {
        Gender = gender;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphSetHeightMessage : BoundUserInterfaceMessage
{
    public float Height;

    public SlimeMorphSetHeightMessage(float height)
    {
        Height = height;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeMorphSetWidthMessage : BoundUserInterfaceMessage
{
    public float Width;

    public SlimeMorphSetWidthMessage(float width)
    {
        Width = width;
    }
}

/// <summary>Commit the pending self-customization edits onto the slime's body (free).</summary>
[Serializable, NetSerializable]
public sealed class SlimeMorphApplyMessage : BoundUserInterfaceMessage;

/// <summary>Discard pending edits, restoring the menu to the look it had when opened (free).</summary>
[Serializable, NetSerializable]
public sealed class SlimeMorphResetMessage : BoundUserInterfaceMessage;

// ---- Mimic / target messages ----

/// <summary>Load a studied target into the editor (or null to clear); updates sliders + preview, no cost.</summary>
[Serializable, NetSerializable]
public sealed class SlimeMorphSelectTargetMessage : BoundUserInterfaceMessage
{
    public NetEntity? Target;

    public SlimeMorphSelectTargetMessage(NetEntity? target)
    {
        Target = target;
    }
}

/// <summary>Commit the currently loaded target look onto the body (costs nutrition + squish).</summary>
[Serializable, NetSerializable]
public sealed class SlimeMorphMimicMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SlimeMorphForgetMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;

    public SlimeMorphForgetMessage(NetEntity target)
    {
        Target = target;
    }
}

/// <summary>Right-side reset: deselect, revert the body to the slime's own look (costs nutrition + squish).</summary>
[Serializable, NetSerializable]
public sealed class SlimeMorphRevertMessage : BoundUserInterfaceMessage;
