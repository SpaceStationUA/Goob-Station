// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Client._Pirate.MalfAI.Theme;
using Content.Shared._Pirate.MalfAI;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;

namespace Content.Client._Pirate.MalfAI.VoiceModulator;

public sealed class MalfVoiceModulatorSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _res = default!;

    private MalfVoiceModulatorWindow? _window;
    private Font? _malfFont;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MalfVoiceModulatorOpenUiEvent>(OnOpenUi);
    }

    private void OnOpenUi(MalfVoiceModulatorOpenUiEvent ev)
    {
        _malfFont ??= MalfUiTheme.GetFont(_res, 12);
        _window ??= new MalfVoiceModulatorWindow(_malfFont);

        _window.OnConfirm -= OnConfirm;
        _window.OnVerbChanged -= OnVerbChanged;
        _window.OnSoundChanged -= OnSoundChanged;
        _window.OnJobIconChanged -= OnJobIconChanged;
        _window.OnToggle -= OnToggle;
        _window.OnAccentToggle -= OnAccentToggle;
        _window.OnCrewSelected -= OnCrewSelected;
        _window.OnRefreshCrew -= OnRefreshCrew;
        _window.OnConfirm += OnConfirm;
        _window.OnVerbChanged += OnVerbChanged;
        _window.OnSoundChanged += OnSoundChanged;
        _window.OnJobIconChanged += OnJobIconChanged;
        _window.OnToggle += OnToggle;
        _window.OnAccentToggle += OnAccentToggle;
        _window.OnCrewSelected += OnCrewSelected;
        _window.OnRefreshCrew += OnRefreshCrew;
        _window.UpdateState(ev.State);
        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void OnCrewSelected(NetEntity target)
        => RaiseNetworkEvent(new MalfVoiceModulatorCopyCrewEvent(target));

    private void OnRefreshCrew()
        => RaiseNetworkEvent(new MalfVoiceModulatorRefreshCrewEvent());

    private void OnConfirm(string name)
    {
        RaiseNetworkEvent(new MalfVoiceModulatorSubmitNameEvent(name));
    }

    private void OnVerbChanged(string? verb)
    {
        RaiseNetworkEvent(new MalfVoiceModulatorChangeVerbEvent(verb));
    }

    private void OnSoundChanged(string? sound)
    {
        RaiseNetworkEvent(new MalfVoiceModulatorChangeSoundEvent(sound));
    }

    private void OnJobIconChanged(string? icon)
    {
        RaiseNetworkEvent(new MalfVoiceModulatorChangeJobIconEvent(icon));
    }

    private void OnToggle()
    {
        RaiseNetworkEvent(new MalfVoiceModulatorToggleEvent());
    }

    private void OnAccentToggle()
    {
        RaiseNetworkEvent(new MalfVoiceModulatorAccentToggleEvent());
    }
}
