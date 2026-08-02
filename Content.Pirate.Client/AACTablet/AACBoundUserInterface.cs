// SPDX-FileCopyrightText: 2025 Impstation contributors

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chat.TypingIndicator;
using Content.Pirate.Shared.AACTablet;
using Content.Pirate.Shared.QuickPhrase;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.AACTablet;

public sealed class AACBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AACWindow? _window;

    private TypingIndicatorSystem? _typing;

    protected override void Open()
    {
        base.Open();
        _window = new AACWindow(Owner);
        _window.OpenCentered();
        _window.OnClose += Close;
        _window.PhraseButtonPressed += OnPhraseButtonPressed;
        _window.Typing += OnTyping;
        _window.SubmitPressed += OnSubmit;
    }

    private void OnPhraseButtonPressed(List<ProtoId<QuickPhrasePrototype>> phraseId)
    {
        SendMessage(new AACTabletSendPhraseMessage(phraseId));
    }

    private void OnTyping()
    {
        _typing ??= EntMan.System<TypingIndicatorSystem>();
        // Pirate: Goob has no alternate typing-indicator API, so use the local entity indicator.
        _typing?.ClientChangedChatText();
    }

    private void OnSubmit()
    {
        _typing ??= EntMan.System<TypingIndicatorSystem>();
        _typing?.ClientSubmittedChatText();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Parent?.RemoveChild(_window);
    }
}
