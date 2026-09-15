using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class ChannelExtractorTests
    {
        [TestCase("[PlayFabCBSManager] Phase 2 fetch complete", "PlayFabCBSManager")]
        [TestCase("[Level.Controller] transition refused", "Level.Controller")]
        [TestCase("[UI-Pages] opened", "UI-Pages")]
        [TestCase("[Boss Fight] started", "Boss Fight")]
        public void Extract_LeadingTag_ReturnsTheName(string message, string expected)
        {
            Assert.That(new ChannelExtractor().Extract(message), Is.EqualTo(expected));
        }

        [TestCase("no tag here")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("text before [Tag] the tag")]
        [TestCase("[] empty")]
        [TestCase("[unclosed tag")]
        public void Extract_WithoutALeadingTag_ReturnsEmpty(string message)
        {
            Assert.That(new ChannelExtractor().Extract(message), Is.Empty);
        }

        [Test]
        public void Extract_IsCachedByPrefix_SoRepeatsAgree()
        {
            var extractor = new ChannelExtractor();

            string first = extractor.Extract("[Net] request 1");
            string second = extractor.Extract("[Net] request 2");
            string third = extractor.Extract("[Net] request 1");

            Assert.That(first, Is.EqualTo("Net"));
            Assert.That(second, Is.EqualTo("Net"));
            Assert.That(third, Is.EqualTo("Net"));
        }

        [Test]
        public void Extract_OnlyLooksAtTheFirstCharacters()
        {
            string padded = new string(' ', ChannelExtractor.MaxPrefixLength) + "[Late] tag";

            Assert.That(new ChannelExtractor(@"\[([\w]+)\]").Extract(padded), Is.Empty);
        }

        [Test]
        public void CustomPattern_WithOneGroup_NamesTheChannel()
        {
            var extractor = new ChannelExtractor(@"^(\w+)>>");

            Assert.That(extractor.IsValid, Is.True);
            Assert.That(extractor.Extract("Audio>> volume set"), Is.EqualTo("Audio"));
        }

        [Test]
        public void CustomPattern_WithoutAGroup_UsesTheWholeMatch()
        {
            var extractor = new ChannelExtractor(@"^\w+");

            Assert.That(extractor.Extract("Gameplay started"), Is.EqualTo("Gameplay"));
        }

        [Test]
        public void InvalidPattern_FallsBackToTheDefault_AndSaysSo()
        {
            var extractor = new ChannelExtractor("[unclosed(");

            Assert.That(extractor.IsValid, Is.False);
            Assert.That(extractor.Pattern, Is.EqualTo("[unclosed("));
            Assert.That(extractor.Extract("[Fallback] still works"), Is.EqualTo("Fallback"));
        }
    }
}
