// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.SlimeMorph;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.SlimeMorph;

public sealed class SlimeMorphBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SlimeMorphWindow? _window;

    public SlimeMorphBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
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
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SlimeMorphUiState morphState)
            _window?.UpdateState(morphState);
    }
}
