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

        private static LogEntry Entry(LogSeverity severity)
        {
            return new LogEntry(LogEntryKind.Log, severity, "message", string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
