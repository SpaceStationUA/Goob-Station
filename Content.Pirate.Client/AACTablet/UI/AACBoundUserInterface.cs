// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chat.TypingIndicator;
using Content.Pirate.Shared.AACTablet;
using Content.Pirate.Shared.QuickPhrase;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Radio;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.AACTablet.UI;

public sealed class AACBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AACWindow? _window;

    private static readonly ProtoId<TypingIndicatorPrototype> AACTypingIndicator = "aac";

    private TypingIndicatorSystem? _typing;

    public AACBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (_window is { Disposed: false })
            _window.Close();

        _window = this.CreateWindow<AACWindow>();
        _window.OnClose += OnSubmit;
        _window.PhraseButtonPressed += OnPhraseButtonPressed;
        _window.Typing += OnTyping;
        _window.SubmitPressed += OnSubmit;
    }

    private void OnPhraseButtonPressed(
        List<ProtoId<QuickPhrasePrototype>> phraseId,
        ProtoId<RadioChannelPrototype>? channel)
    {
        SendMessage(new AACTabletSendPhraseMessage(phraseId, channel));
    }

    private void OnTyping()
    {
        _typing ??= EntMan.System<TypingIndicatorSystem>();
        _typing?.ClientAlternateTyping(AACTypingIndicator);
    }

    private void OnSubmit()
    {
        _typing ??= EntMan.System<TypingIndicatorSystem>();
        _typing?.ClientSubmittedChatText();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not AACTabletUpdateChannelsMessage msg)
            return;

        _window?.Update(msg);
    }
}
