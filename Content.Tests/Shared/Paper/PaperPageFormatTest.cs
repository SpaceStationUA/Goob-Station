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

        [Test]
        public void SanitizeStripsEveryReservedControlCharacter()
        {
            // \f breaks leaves, \u001D/\u001E forge signatures, \u001C/\u001F forge the cover meta block.
            const string forged = "ok\f text\u001Dname\u001Eevil\u001Cskin\u001FBookX";

            var clean = PaperPageFormat.Sanitize(forged);

            Assert.That(clean, Is.EqualTo("ok textnameevilskinBookX"));
        }

        [Test]
        public void SanitizeLeavesOrdinaryTextAlone()
        {
            const string text = "plain text with \n newlines, табличний текст і emoji \u2764";

            Assert.That(PaperPageFormat.Sanitize(text), Is.EqualTo(text));
            Assert.That(PaperPageFormat.Sanitize(string.Empty), Is.Empty);
        }

        [Test]
        public void SanitizedMarkerStaysPlainTextWhenStored()
        {
            // A forged marker in player text must not become real document structure.
            var pages = new List<PaperPage>
            {
                new() { Content = PaperPageFormat.Sanitize("a\u001Dforged name") },
            };

            var decoded = PaperPageFormat.Decode(PaperPageFormat.Encode(pages));

            Assert.That(decoded, Has.Count.EqualTo(1));
            Assert.That(decoded[0].Content, Is.EqualTo("aforged name"));
            Assert.That(decoded[0].Signatures, Is.Empty);
        }
    }
}
