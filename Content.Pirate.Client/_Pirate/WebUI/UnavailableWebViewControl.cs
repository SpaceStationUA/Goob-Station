// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
/// Native placeholder while the launcher's WebView module is incompatible with engine 292.
/// Keeps existing window layouts usable without loading CEF or executing web content.
/// </summary>
public sealed class UnavailableWebViewControl : PanelContainer
{
    public string Url { get; set; } = string.Empty;
    public bool AlwaysActive { get; set; }

    public UnavailableWebViewControl()
    {
        CanKeyboardFocus = true;
        PanelOverride = new StyleBoxFlat(Color.White);
        AddChild(new Label
        {
            Text = Loc.GetString("pirate-webui-no-signal"),
            FontColorOverride = Color.Black,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        });
    }

    // No browser is created: retain the bridge boundary for when a compatible module is available.
    public void ExecuteJavaScript(string code) { }
    public void AddBeforeBrowseHandler(Action<IWebUiBrowseContext> handler) { }
    public void AddResourceRequestHandler(Action<IWebUiRequestContext> handler) { }
}

public interface IWebUiBrowseContext
{
    string Url { get; }
    void DoCancel();
}

public interface IWebUiRequestContext
{
    string Url { get; }
    void DoRespondStream(Stream stream, string contentType);
}
