using System;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    public sealed class LogStoreTests
    {
        [Test]
        public void Append_AssignsIncreasingIdsStartingAtOne()
        {
            var store = new LogStore(capacity: 8);

            store.Append(Entry(LogSeverity.Log));
            store.Append(Entry(LogSeverity.Log));

            Assert.That(store.Entries.Select(e => e.Id), Is.EqualTo(new long[] { 1, 2 }));
        }

        [Test]
        public void Append_StampsCurrentSession()
        {
            var store = new LogStore(capacity: 8) { CurrentSession = 3 };

            store.Append(Entry(LogSeverity.Log));

            Assert.That(store[0].Session, Is.EqualTo(3));
        }

        [Test]
        public void SeverityCounts_FollowAppendsAndEvictions()
        {
            var store = new LogStore(capacity: 2);

            store.Append(Entry(LogSeverity.Error));
            store.Append(Entry(LogSeverity.Warning));
            store.Append(Entry(LogSeverity.Log));

            Assert.That(store.CountOf(LogSeverity.Error), Is.EqualTo(0), "evicted");
            Assert.That(store.CountOf(LogSeverity.Warning), Is.EqualTo(1));
            Assert.That(store.CountOf(LogSeverity.Log), Is.EqualTo(1));
            Assert.That(store.Overwritten, Is.EqualTo(1));
        }

        [Test]
        public void Markers_AreNotCountedAsSeverities()
        {
            var store = new LogStore(capacity: 8);

            store.Append(LogEntry.Marker("Entered Play mode", DateTime.UtcNow, 0));

            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.CountOf(LogSeverity.Log), Is.EqualTo(0));
        }

        [Test]
        public void Clear_DropsEntriesAndCountsButKeepsIdSequence()
        {
            var store = new LogStore(capacity: 8);
            store.Append(Entry(LogSeverity.Error));

            store.Clear();
            store.Append(Entry(LogSeverity.Log));

            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.CountOf(LogSeverity.Error), Is.EqualTo(0));
            Assert.That(store[0].Id, Is.EqualTo(2));
        }

        [Test]
        public void Events_FireOnAppendAndClear()
        {
            var store = new LogStore(capacity: 8);
            LogEntry appended = null;
            bool cleared = false;
            store.EntryAppended += e => appended = e;
            store.Cleared += () => cleared = true;

            LogEntry entry = Entry(LogSeverity.Log);
            store.Append(entry);
            store.Clear();

            Assert.That(appended, Is.SameAs(entry));
            Assert.That(cleared, Is.True);
        }

        [Test]
        public void Append_Null_Throws()
        {
            var store = new LogStore(capacity: 8);

            Assert.Throws<ArgumentNullException>(() => store.Append(null));
        }

        [Test]
        public void Restore_KeepsIdsAndSessions_ContinuesSequence_AndRaisesNoAppendEvent()
        {
            var store = new LogStore(capacity: 8);
            int appended = 0;
            store.EntryAppended += _ => appended++;

            LogEntry first = Entry(LogSeverity.Error);
            first.AssignSequence(41, 2);
            LogEntry second = Entry(LogSeverity.Log);
            second.AssignSequence(42, 5);
            store.Restore(first);
            store.Restore(second);
            store.Append(Entry(LogSeverity.Warning));

            Assert.That(appended, Is.EqualTo(1));
            Assert.That(store.Entries.Select(e => e.Id), Is.EqualTo(new long[] { 41, 42, 43 }));
            Assert.That(store[2].Session, Is.EqualTo(5), "session continues from the highest restored value");
            Assert.That(store.CountOf(LogSeverity.Error), Is.EqualTo(1));
            Assert.That(store.CountOf(LogSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void Restore_BeyondCapacity_KeepsTheTail()
        {
            var store = new LogStore(capacity: 2);
            for (int i = 1; i <= 3; i++)
            {
                LogEntry entry = Entry(LogSeverity.Log);
                entry.AssignSequence(i, 0);
                store.Restore(entry);
            }

            Assert.That(store.Entries.Select(e => e.Id), Is.EqualTo(new long[] { 2, 3 }));
            Assert.That(store.CountOf(LogSeverity.Log), Is.EqualTo(2));
        }

        [Test]
        public void Channels_AreAssignedOnInsert_AndCounted()
        {
            var store = new LogStore(capacity: 8) { ChannelExtractor = new ChannelExtractor() };

            store.Append(Message("[Net] one"));
            store.Append(Message("[Net] two"));
            store.Append(Message("[UI] three"));
            store.Append(Message("untagged"));
            store.Append(LogEntry.Marker("[Net] markers never get a channel", DateTime.UtcNow, 0));

            Assert.That(store[0].Channel, Is.EqualTo("Net"));
            Assert.That(store[3].Channel, Is.Empty);
            Assert.That(store[4].Channel, Is.Empty, "markers are not channelled");
            Assert.That(store.CountOfChannel("Net"), Is.EqualTo(2));
            Assert.That(store.CountOfChannel("UI"), Is.EqualTo(1));
            Assert.That(store.ChannelCounts.Select(c => c.Key), Is.EquivalentTo(new[] { "Net", "UI" }));
        }

        [Test]
        public void ChannelCounts_FollowEvictionsAndClear()
        {
            var store = new LogStore(capacity: 2) { ChannelExtractor = new ChannelExtractor() };

            store.Append(Message("[Net] one"));
            store.Append(Message("[UI] two"));
            store.Append(Message("[UI] three"));

            Assert.That(store.CountOfChannel("Net"), Is.EqualTo(0), "the evicted entry gave up its count");
            Assert.That(store.CountOfChannel("UI"), Is.EqualTo(2));

            store.Clear();
            Assert.That(store.ChannelCounts, Is.Empty);
        }

        [Test]
        public void ReassignChannels_RewritesEveryEntry_AndRaisesTheEvent()
        {
            var store = new LogStore(capacity: 8) { ChannelExtractor = new ChannelExtractor() };
            store.Append(Message("Net>> one"));
            store.Append(Message("[Net] two"));
            bool raised = false;
            store.ChannelsReassigned += () => raised = true;

            store.ReassignChannels(new ChannelExtractor(@"^(\w+)>>"));

            Assert.That(raised, Is.True);
            Assert.That(store[0].Channel, Is.EqualTo("Net"));
            Assert.That(store[1].Channel, Is.Empty);
            Assert.That(store.CountOfChannel("Net"), Is.EqualTo(1));
        }

        [Test]
        public void TagRules_TagMessagesWithoutAPrefix_WhilePrefixesStillWin()
        {
            var rules = new TagRuleSet(new[] { new TagRule("Backend", TagMatch.Contains, "PlayFab") });
            var store = new LogStore(capacity: 8) { ChannelExtractor = new ChannelExtractor(), TagRules = rules };

            store.Append(Message("PlayFab timed out"));
            store.Append(Message("[Net] PlayFab handshake"));
            store.Append(Message("Firebase timed out"));

            Assert.That(store[0].Channel, Is.EqualTo("Backend"));
            Assert.That(store[1].Channel, Is.EqualTo("Net"), "an explicit prefix beats a rule");
            Assert.That(store[2].Channel, Is.Empty);
            Assert.That(store.CountOfChannel("Backend"), Is.EqualTo(1));
        }

        [Test]
        public void AutoTagByCaller_NamesTheCallingType_OnlyWhenNothingElseApplies()
        {
            const string trace = "UnityEngine.Debug:Log (object)\nGame.Boss.EnemySpawner:Activate () (at Assets/Game/EnemySpawner.cs:4)";
            var store = new LogStore(capacity: 8)
            {
                ChannelExtractor = new ChannelExtractor(),
                TagRules = new TagRuleSet(new[] { new TagRule("Backend", TagMatch.Contains, "PlayFab") }),
                AutoTagByCaller = true,
            };

            store.Append(new LogEntry(LogEntryKind.Log, LogSeverity.Log, "Activated", trace, DateTime.UtcNow, 0, 1, true, ObjectRef.None));
            store.Append(new LogEntry(LogEntryKind.Log, LogSeverity.Log, "PlayFab ok", trace, DateTime.UtcNow, 0, 1, true, ObjectRef.None));
            store.Append(new LogEntry(LogEntryKind.Log, LogSeverity.Log, "[Net] hello", trace, DateTime.UtcNow, 0, 1, true, ObjectRef.None));
            store.Append(Message("no stack at all"));

            Assert.That(store[0].Channel, Is.EqualTo("EnemySpawner"));
            Assert.That(store[1].Channel, Is.EqualTo("Backend"), "a rule beats the caller");
            Assert.That(store[2].Channel, Is.EqualTo("Net"), "a prefix beats everything");
            Assert.That(store[3].Channel, Is.Empty, "no caller, no tag");
        }

        [Test]
        public void ReassignChannels_WithANewPolicy_RetagsEverything()
        {
            var store = new LogStore(capacity: 8) { ChannelExtractor = new ChannelExtractor() };
            store.Append(Message("PlayFab timed out"));
            Assert.That(store[0].Channel, Is.Empty);

            store.ReassignChannels(new ChannelExtractor(), new TagRuleSet(new[] { new TagRule("Backend", TagMatch.Contains, "PlayFab") }), false);
            Assert.That(store[0].Channel, Is.EqualTo("Backend"));
            Assert.That(store.CountOfChannel("Backend"), Is.EqualTo(1));

            store.ReassignChannels(new ChannelExtractor(), TagRuleSet.Empty, false);
            Assert.That(store[0].Channel, Is.Empty);
            Assert.That(store.ChannelCounts, Is.Empty);
        }

        [Test]
        public void WithoutAnExtractor_ChannelsStayEmpty()
        {
            var store = new LogStore(capacity: 8);

            store.Append(Message("[Net] one"));

            Assert.That(store[0].Channel, Is.Empty);
            Assert.That(store.ChannelCounts, Is.Empty);
        }

        [Test]
        public void Restore_AlsoAssignsChannels()
        {
            var store = new LogStore(capacity: 8) { ChannelExtractor = new ChannelExtractor() };
            LogEntry entry = Message("[Journal] restored");
            entry.AssignSequence(12, 1);

            store.Restore(entry);

            Assert.That(store[0].Channel, Is.EqualTo("Journal"));
            Assert.That(store.CountOfChannel("Journal"), Is.EqualTo(1));
        }

        private static LogEntry Message(string message)
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }

        private static LogEntry Entry(LogSeverity severity)
        {
            return new LogEntry(LogEntryKind.Log, severity, "message", string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
