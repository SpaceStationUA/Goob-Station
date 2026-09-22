// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Fragments;
using Content.Pirate.Client.Radio;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Pirate.Client.CartridgeLoader.Cartridges;

/// <summary>
///     PID internet-radio program. The actual CEF playback control is owned
///     by <see cref="PirateRadioClientSystem"/> and survives program close;
///     this fragment merely hosts it while open.
/// </summary>
public sealed partial class RadioUi : UIFragment
{
    private PirateRadioClientSystem? _system;
    private RadioHostPanel? _root;
    private NetEntity _marker = NetEntity.Invalid;

    public override Control GetUIFragmentRoot() => _root!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        // The loader BUI re-runs Setup on state pushes; a live root means we
        // are already set up (the BUI's attach guard skips those anyway).
        if (_root is { Disposed: false })
            return;

        _marker = fragmentOwner is { } owner && owner.IsValid()
            ? IoCManager.Resolve<IEntityManager>().GetNetEntity(owner)
            : NetEntity.Invalid;

        _system = EntitySystem.Get<PirateRadioClientSystem>();
        var view = _system.EnsurePlayback(_marker);
        // Pushes done while the fragment was elsewhere (theme picker) may
        // not have reached the detached page; re-sync fully.
        _system.OnFragmentAttached(_marker);

        _root = new RadioHostPanel
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            KeepAlive = view,
        };
        if (view != null)
        {
            _root.AddChild(view);
            // Scale changes rebuild the webview; the fragment object survives
            // (KeepAlive swaps must land in the panel the radio system owns).
            _system.RegisterHostPanel(_marker, _root, view);
        }
        else
            _root.AddChild(new Label { Text = "Radio unavailable (headless)." });
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        // State arrives via raw network events, not the BUI; nothing to do.
    }

    /// <summary>
    ///     Host panel that detaches the persistent webview when disposed, so
    ///     closing the program does not destroy the CEF browser (the audio
    ///     would stop; the control could also never be re-hosted).
    /// </summary>
    /// <summary>What the radio system sees of the hosting panel (scale
    /// rebuilds re-attach a fresh webview under a fresh KeepAlive).</summary>
    public interface IRadioWebviewHost
    {
        Control? KeepAlive { get; set; }
    }

    private sealed class RadioHostPanel : PanelContainer, IRadioWebviewHost
    {
        Control? IRadioWebviewHost.KeepAlive
        {
            get => KeepAlive;
            set => KeepAlive = value;
        }

        public Control? KeepAlive;

        protected override void Dispose(bool disposing)
        {
            if (KeepAlive != null && KeepAlive.Parent == this)
                RemoveChild(KeepAlive);
            base.Dispose(disposing);
        }
    }
}
