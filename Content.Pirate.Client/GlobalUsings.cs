// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

global using System;
global using System.Collections.Generic;
global using Robust.Shared.Analyzers;
global using Robust.Shared.Log;
global using Robust.Shared.Localization;
global using Robust.Shared.GameObjects;
global using Robust.Shared.IoC;
global using Robust.Shared.Maths;
global using Robust.Shared.ViewVariables;
global using Robust.Shared.Serialization.Manager.Attributes;

// Pirate: temporarily keep web windows native until the launcher provides a compatible WebView module.
global using WebViewControl = Content.Pirate.Client._Pirate.WebUI.UnavailableWebViewControl;
global using IBeforeBrowseContext = Content.Pirate.Client._Pirate.WebUI.IWebUiBrowseContext;
global using IRequestHandlerContext = Content.Pirate.Client._Pirate.WebUI.IWebUiRequestContext;
