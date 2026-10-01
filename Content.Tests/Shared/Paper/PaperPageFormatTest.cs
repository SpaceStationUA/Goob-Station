// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Paper;
using Content.Shared.Paper;
using NUnit.Framework;

namespace Content.Tests.Shared.Paper
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public sealed class PaperPageFormatTest
    {
        [Test]
        public void RoundTripsLeavesAndSignatures()
        {
            var pages = new List<PaperPage>
            {
                new() { Content = "first leaf" },
                new() { Content = "second leaf" },
            };
            pages[1].Signatures.Add(new StampDisplayInfo
            {
                StampedName = "John Doe",
                StampedColor = PaperPageFormat.SignatureColor,
            });
            pages[1].Signatures.Add(new StampDisplayInfo
            {
                StampedName = "Jane Roe",
                StampedColor = PaperPageFormat.SignatureColor,
            });

            var decoded = PaperPageFormat.Decode(PaperPageFormat.Encode(pages));

            Assert.That(decoded, Has.Count.EqualTo(2));
            Assert.That(decoded[0].Content, Is.EqualTo("first leaf"));
            Assert.That(decoded[0].Signatures, Is.Empty);
            Assert.That(decoded[1].Content, Is.EqualTo("second leaf"));
            Assert.That(decoded[1].Signatures.Select(stamp => stamp.StampedName),
                Is.EqualTo(new[] { "John Doe", "Jane Roe" }));
        }

        [Test]
        public void LegacyDocumentBecomesSingleLeaf()
        {
            // Diaries saved before paging existed decode into one leaf holding everything.
            const string oldContent = "an old diary entry\nwith two lines";
            var decoded = PaperPageFormat.Decode(oldContent);

            Assert.That(decoded, Has.Count.EqualTo(1));
            Assert.That(decoded[0].Content, Is.EqualTo(oldContent));
            Assert.That(decoded[0].Signatures, Is.Empty);
        }

        [Test]
        public void EmptyDocumentIsOneBlankLeaf()
        {
            var decoded = PaperPageFormat.Decode(string.Empty);

            Assert.That(decoded, Has.Count.EqualTo(1));
            Assert.That(decoded[0].Content, Is.Empty);
        }

        [Test]
        public void BlankLeafBetweenTextKeepsItsPlace()
        {
            var pages = new List<PaperPage>
            {
                new() { Content = "a" },
                new(),
                new() { Content = "c" },
            };

            var decoded = PaperPageFormat.Decode(PaperPageFormat.Encode(pages));

            Assert.That(decoded.Select(page => page.Content), Is.EqualTo(new[] { "a", "", "c" }));
        }
    }
}
