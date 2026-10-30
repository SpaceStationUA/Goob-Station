// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Pirate.Chat;

/// <summary>
/// Keeps runechat resources on their owning client's UI thread and lifetime.
/// </summary>
public sealed class RunechatStyleCacheController : UIController
{
    internal readonly Dictionary<(string, int), Font> Fonts = new();
    internal readonly Dictionary<(string, int), Stylesheet> Sheets = new();
}
