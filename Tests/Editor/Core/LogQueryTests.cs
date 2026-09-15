using System;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class LogQueryTests
    {
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Parse_EmptyText_MatchesEverything(string text)
        {
            LogQuery query = LogQuery.Parse(text);

            Assert.That(query.IsEmpty, Is.True);
            Assert.That(query.Error, Is.Null);
            Assert.That(query.Matches(Entry("anything at all")), Is.True);
        }

        [Test]
        public void BareTerms_AreCaseInsensitiveSubstrings_JoinedByAnd()
        {
            LogQuery query = LogQuery.Parse("timeout inventory");

            Assert.That(query.ClauseCount, Is.EqualTo(2));
            Assert.That(query.Matches(Entry("Request TIMEOUT while fetching Inventory")), Is.True);
            Assert.That(query.Matches(Entry("Request timeout while fetching profile")), Is.False);
        }

        [Test]
        public void QuotedPhrase_MatchesTheWholePhrase()
        {
            LogQuery query = LogQuery.Parse("\"connection reset\"");

            Assert.That(query.Matches(Entry("Socket: connection reset by peer")), Is.True);
            Assert.That(query.Matches(Entry("connection was reset")), Is.False);
        }

        [Test]
        public void Exclusion_RemovesMatchingEntries_AndWorksWithPhrases()
        {
            LogQuery query = LogQuery.Parse("timeout -\"retry scheduled\"");

            Assert.That(query.Matches(Entry("timeout on GetInventory")), Is.True);
            Assert.That(query.Matches(Entry("timeout on GetInventory, retry scheduled")), Is.False);
        }

        [Test]
        public void Regex_MatchesWithFlags_AndSpacesInsideThePattern()
        {
            LogQuery query = LogQuery.Parse("/timeout \\d+ms/i");

            Assert.That(query.Error, Is.Null);
            Assert.That(query.Matches(Entry("Request TIMEOUT 8000ms")), Is.True);
            Assert.That(query.Matches(Entry("Request timeout soon")), Is.False);
        }

        [Test]
        public void Regex_IsCaseSensitiveWithoutTheIFlag()
        {
            Assert.That(LogQuery.Parse("/Timeout/").Matches(Entry("timeout")), Is.False);
            Assert.That(LogQuery.Parse("/Timeout/").Matches(Entry("Timeout")), Is.True);
        }

        [Test]
        public void NegatedRegex_Excludes()
        {
            LogQuery query = LogQuery.Parse("-/^\\[UI\\]/");

            Assert.That(query.Matches(Entry("[UI] opened")), Is.False);
            Assert.That(query.Matches(Entry("[Net] opened")), Is.True);
        }

        [Test]
        public void Severity_AcceptsAList_AndAliases()
        {
            LogQuery query = LogQuery.Parse("sev:error,warning");

            Assert.That(query.Matches(Entry("a", LogSeverity.Error)), Is.True);
            Assert.That(query.Matches(Entry("a", LogSeverity.Warning)), Is.True);
            Assert.That(query.Matches(Entry("a", LogSeverity.Log)), Is.False);
            Assert.That(LogQuery.Parse("sev:warn").Matches(Entry("a", LogSeverity.Warning)), Is.True);
        }

        [Test]
        public void Channel_SupportsGlobs_AndIsCaseInsensitive()
        {
            LogQuery query = LogQuery.Parse("tag:PlayFab*");

            Assert.That(query.Matches(Channelled("PlayFabCBSManager")), Is.True);
            Assert.That(query.Matches(Channelled("playfabweapons")), Is.True);
            Assert.That(query.Matches(Channelled("LevelController")), Is.False);
            Assert.That(query.Matches(Entry("untagged")), Is.False);
        }

        [Test]
        public void NegatedChannel_KeepsUntaggedEntries()
        {
            LogQuery query = LogQuery.Parse("-tag:UI");

            Assert.That(query.Matches(Channelled("UI")), Is.False);
            Assert.That(query.Matches(Channelled("Net")), Is.True);
            Assert.That(query.Matches(Entry("untagged")), Is.True);
        }

        [Test]
        public void InStack_ExtendsTextAndRegexToTheStackTrace()
        {
            LogEntry entry = new LogEntry(
                LogEntryKind.Log, LogSeverity.Log, "nothing useful here", "Game.Boss:Update () (at Assets/Boss.cs:12)",
                DateTime.UtcNow, 0, 1, true, ObjectRef.None);

            Assert.That(LogQuery.Parse("Boss.cs").Matches(entry), Is.False);
            Assert.That(LogQuery.Parse("Boss.cs in:stack").Matches(entry), Is.True);
            Assert.That(LogQuery.Parse("in:stack /Assets\\/Boss/").Matches(entry), Is.True);
        }

        [Test]
        public void Or_MatchesEitherSide_AndBindsTighterThanAnd()
        {
            LogQuery query = LogQuery.Parse("sev:error OR tag:Boss");

            Assert.That(query.ClauseCount, Is.EqualTo(1));
            Assert.That(query.Matches(Entry("a", LogSeverity.Error)), Is.True);
            Assert.That(query.Matches(Channelled("Boss")), Is.True);
            Assert.That(query.Matches(Entry("a")), Is.False);

            LogQuery mixed = LogQuery.Parse("timeout error OR warning");
            Assert.That(mixed.ClauseCount, Is.EqualTo(2), "'timeout' AND ('error' OR 'warning')");
            Assert.That(mixed.Matches(Entry("timeout error")), Is.True);
            Assert.That(mixed.Matches(Entry("timeout warning")), Is.True);
            Assert.That(mixed.Matches(Entry("plain error")), Is.False);
        }

        [Test]
        public void ColonInAnOrdinaryTerm_StaysATextSearch()
        {
            LogQuery query = LogQuery.Parse("https://example.com/path");

            Assert.That(query.Error, Is.Null);
            Assert.That(query.Matches(Entry("opening https://example.com/path now")), Is.True);
        }

        [TestCase("\"unclosed", "closing quote")]
        [TestCase("/unclosed", "closing slash")]
        [TestCase("/bad[/", "not valid")]
        [TestCase("/foo/z", "flag")]
        [TestCase("sev:nonsense", "Unknown severity")]
        [TestCase("sev:", "at least one severity")]
        [TestCase("tag:", "needs a channel")]
        [TestCase("in:file", "only supports")]
        [TestCase("OR foo", "before it")]
        [TestCase("foo OR", "after it")]
        public void MalformedQueries_ExplainThemselves_AndMatchNothing(string text, string expectedFragment)
        {
            LogQuery query = LogQuery.Parse(text);

            Assert.That(query.Error, Is.Not.Null, text);
            Assert.That(query.Error, Does.Contain(expectedFragment));
            Assert.That(query.Matches(Entry("anything")), Is.False);
        }

        [Test]
        public void Tokenize_KeepsPhrasesAndRegexWhole()
        {
            var tokens = LogQuery.Tokenize("a \"two words\" -b /re gex/i tag:X", out string error);

            Assert.That(error, Is.Null);
            Assert.That(tokens, Is.EqualTo(new[] { "a", "\"two words\"", "-b", "/re gex/i", "tag:X" }));
        }

        [Test]
        public void Text_IsExposedForRoundTripping()
        {
            Assert.That(LogQuery.Parse("  timeout  ").Text, Is.EqualTo("  timeout  "));
        }

        private static LogEntry Entry(string message, LogSeverity severity = LogSeverity.Log)
        {
            return new LogEntry(LogEntryKind.Log, severity, message, string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }

        private static LogEntry Channelled(string channel)
        {
            LogEntry entry = Entry("[" + channel + "] message");
            entry.Channel = channel;
            return entry;
        }
    }
}
