// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chat.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._Pirate.Chat;

[TestFixture]
public sealed class RunechatStyleTest
{
    [TestCase(RunechatStyle.FontPath, 12)]
    [TestCase(RunechatStyle.WhisperFontPath, 6)]
    public async Task FontAndStylesheetCachesBelongToEachClient(string path, int size)
    {
        await using var first = await PoolManager.GetServerClient();
        await using var second = await PoolManager.GetServerClient();
        Font firstFont = default!;
        Font secondFont = default!;
        Stylesheet firstSheet = default!;
        Stylesheet secondSheet = default!;

        await Task.WhenAll(
            first.Client.WaitPost(() =>
            {
                firstFont = RunechatStyle.GetFont(path, size);
                firstSheet = RunechatStyle.GetStylesheet(path, size);
                Assert.That(RunechatStyle.GetFont(path, size), Is.SameAs(firstFont));
                Assert.That(RunechatStyle.GetStylesheet(path, size), Is.SameAs(firstSheet));
            }),
            second.Client.WaitPost(() =>
            {
                secondFont = RunechatStyle.GetFont(path, size);
                secondSheet = RunechatStyle.GetStylesheet(path, size);
                Assert.That(RunechatStyle.GetFont(path, size), Is.SameAs(secondFont));
                Assert.That(RunechatStyle.GetStylesheet(path, size), Is.SameAs(secondSheet));
            }));

        Assert.That(firstFont, Is.Not.SameAs(secondFont));
        Assert.That(firstSheet, Is.Not.SameAs(secondSheet));

        await first.CleanReturnAsync();
        await second.CleanReturnAsync();
    }
}
