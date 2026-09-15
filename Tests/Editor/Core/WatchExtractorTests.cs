using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class WatchExtractorTests
    {
        [TestCase("[watch:PlayerHP] 87", "PlayerHP")]
        [TestCase("[watch:Enemy.Count] 12", "Enemy.Count")]
        [TestCase("[watch:Frame Time] 16.7ms", "Frame Time")]
        public void Extract_ReadsTheWatchKey(string message, string expected)
        {
            Assert.That(new WatchExtractor().Extract(message), Is.EqualTo(expected));
        }

        [TestCase("[PlayerHP] 87")]
        [TestCase("watch:PlayerHP 87")]
        [TestCase("some log about watch: things")]
        [TestCase("")]
        public void Extract_WithoutAWatchPrefix_ReturnsEmpty(string message)
        {
            Assert.That(new WatchExtractor().Extract(message), Is.Empty);
        }

        [Test]
        public void DefaultPattern_DoesNotCollideWithChannels()
        {
            Assert.That(new ChannelExtractor().Extract("[watch:PlayerHP] 87"), Is.Empty, "a colon is not part of a channel name");
            Assert.That(new WatchExtractor().Extract("[Net] request"), Is.Empty);
        }

        [Test]
        public void CustomPattern_Works_AndAnInvalidOneFallsBack()
        {
            Assert.That(new WatchExtractor(@"^(\w+)=").Extract("PlayerHP=87"), Is.EqualTo("PlayerHP"));

            var broken = new WatchExtractor("[unclosed(");
            Assert.That(broken.IsValid, Is.False);
            Assert.That(broken.Extract("[watch:PlayerHP] 87"), Is.EqualTo("PlayerHP"));
        }
    }
}
