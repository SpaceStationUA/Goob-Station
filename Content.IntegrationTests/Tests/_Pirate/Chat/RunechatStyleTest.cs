// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Chat.UI;
using Content.IntegrationTests.Pair;
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
        TestPair first = null;
        TestPair second = null;
        try
        {
            first = await PoolManager.GetServerClient();
            second = await PoolManager.GetServerClient();
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

            // A returned pair may already belong to another test while the other return awaits.
            await first.CleanReturnAsync();
            first = null;
            await second.CleanReturnAsync();
            second = null;
        }
        finally
        {
            try
            {
                if (second != null)
                    await second.DisposeAsync();
            }
            finally
            {
                if (first != null)
                    await first.DisposeAsync();
            }
        }
    }
}
