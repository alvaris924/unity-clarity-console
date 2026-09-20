using System;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;

namespace ClarityConsole.Tests.UI
{
    internal sealed class TagSuggestionTests
    {
        [Test]
        public void ForAnEntryWithACaller_ProposesTheCallingClass()
        {
            LogEntry entry = Entry("Activated: Pistol x6, wave=0", "UnityEngine.Debug:Log (object)\nGame.EnemySpawner:Activate () (at Assets/Game/EnemySpawner.cs:44)");

            TagSuggestion suggestion = TagSuggestion.For(entry);

            Assert.That(suggestion.Tag, Is.EqualTo("EnemySpawner"));
            Assert.That(suggestion.Match, Is.EqualTo(TagMatch.Caller));
            Assert.That(suggestion.Pattern, Is.EqualTo("EnemySpawner"));
            Assert.That(suggestion.PatternFor(TagMatch.Contains), Is.EqualTo("Activated Pistol"), "the message start is ready for a switch to a text match");
        }

        [Test]
        public void ForAnEntryWithoutACaller_ProposesTheMessageStart()
        {
            TagSuggestion suggestion = TagSuggestion.For(Entry("Client connected: NamedPipe-3"));

            Assert.That(suggestion.Match, Is.EqualTo(TagMatch.Contains));
            Assert.That(suggestion.Tag, Is.EqualTo("Client connected"));
            Assert.That(suggestion.Pattern, Is.EqualTo("Client connected"));
            Assert.That(suggestion.PatternFor(TagMatch.Caller), Is.Empty);
        }

        [TestCase("Sent handshake (unity-mcp protocol v2.0, tools=52)", "Sent handshake (unity-mcp")]
        [TestCase("[TIMING] Validation took 25ms", "[TIMING] Validation took")]
        [TestCase("Cleaned 12 expired entries", "Cleaned")]
        [TestCase("Score: 1200", "Score")]
        [TestCase("first line\nsecond line", "first line")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        public void MessageStart_TakesAFewWords_AndStopsAtNumbers(string message, string expected)
        {
            Assert.That(TagSuggestion.MessageStart(message), Is.EqualTo(expected));
        }

        private static LogEntry Entry(string message, string trace = "")
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, trace, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
