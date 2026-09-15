using System;
using System.IO;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class LogJournalTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ClarityConsoleJournalTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void Load_OnMissingDirectory_ReturnsNothing()
        {
            using var journal = new LogJournal(_directory);

            Assert.That(journal.Load(dropContexts: false), Is.Empty);
            Assert.That(journal.SizeBytes, Is.EqualTo(0));
        }

        [Test]
        public void Append_ThenLoad_RoundTripsEveryField()
        {
            var store = new LogStore(capacity: 16) { CurrentSession = 3 };
            var log = new LogEntry(LogEntryKind.Log, LogSeverity.Exception, "boom é中\U0001F600", "Foo:Bar () (at Assets/Foo.cs:12)\nBaz:Qux ()", new DateTime(2026, 9, 15, 10, 30, 0, DateTimeKind.Utc), 1234, 7, false, new ObjectRef(-4242));
            var marker = LogEntry.Marker("Entered Play mode", new DateTime(2026, 9, 15, 10, 31, 0, DateTimeKind.Utc), 1300);
            store.Append(log);
            store.Append(marker);

            using (var journal = new LogJournal(_directory))
            {
                journal.Append(log);
                journal.Append(marker);
                journal.Flush();
            }

            using var reader = new LogJournal(_directory);
            LogEntry[] restored = reader.Load(dropContexts: false).ToArray();

            Assert.That(restored.Length, Is.EqualTo(2));
            LogEntry r = restored[0];
            Assert.That(r.Kind, Is.EqualTo(LogEntryKind.Log));
            Assert.That(r.Severity, Is.EqualTo(LogSeverity.Exception));
            Assert.That(r.Message, Is.EqualTo(log.Message));
            Assert.That(r.StackTrace, Is.EqualTo(log.StackTrace));
            Assert.That(r.TimestampUtc, Is.EqualTo(log.TimestampUtc));
            Assert.That(r.TimestampUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(r.Frame, Is.EqualTo(1234));
            Assert.That(r.ThreadId, Is.EqualTo(7));
            Assert.That(r.IsMainThread, Is.False);
            Assert.That(r.Context.InstanceId, Is.EqualTo(-4242));
            Assert.That(r.Session, Is.EqualTo(3));
            Assert.That(r.Id, Is.EqualTo(1));
            Assert.That(restored[1].Kind, Is.EqualTo(LogEntryKind.Marker));
            Assert.That(restored[1].Message, Is.EqualTo("Entered Play mode"));
            Assert.That(restored[1].Id, Is.EqualTo(2));
            Assert.That(reader.SizeBytes, Is.GreaterThan(0));
        }

        [Test]
        public void Load_WithDropContexts_ClearsObjectReferences()
        {
            using (var journal = new LogJournal(_directory))
            {
                journal.Append(Sequenced(Entry("with context", new ObjectRef(99)), 1));
            }

            using var reader = new LogJournal(_directory);
            LogEntry restored = reader.Load(dropContexts: true).Single();

            Assert.That(restored.Context.HasValue, Is.False);
        }

        [Test]
        public void Append_AfterLoad_ContinuesTheSameSegmentInOrder()
        {
            using (var journal = new LogJournal(_directory))
            {
                journal.Append(Sequenced(Entry("one"), 1));
            }

            using (var journal = new LogJournal(_directory))
            {
                journal.Load(dropContexts: false);
                journal.Append(Sequenced(Entry("two"), 2));
            }

            using var reader = new LogJournal(_directory);
            Assert.That(reader.Load(dropContexts: false).Select(e => e.Message), Is.EqualTo(new[] { "one", "two" }));
            Assert.That(Directory.GetFiles(_directory).Length, Is.EqualTo(1), "no rotation below the segment limit");
        }

        [Test]
        public void Rotation_KeepsOnlyTheNewestSegments()
        {
            const int maxSegmentBytes = 600;
            const int maxSegments = 2;
            using (var journal = new LogJournal(_directory, maxSegmentBytes, maxSegments))
            {
                for (int i = 1; i <= 40; i++)
                {
                    journal.Append(Sequenced(Entry("entry number " + i.ToString("D3")), i));
                }
            }

            string[] files = Directory.GetFiles(_directory);
            Assert.That(files.Length, Is.EqualTo(maxSegments));
            Assert.That(files.All(f => new FileInfo(f).Length <= maxSegmentBytes + 200), "segments stay near the limit");

            using var reader = new LogJournal(_directory, maxSegmentBytes, maxSegments);
            LogEntry[] restored = reader.Load(dropContexts: false).ToArray();

            Assert.That(restored.Length, Is.GreaterThan(0).And.LessThan(40));
            Assert.That(restored.Last().Message, Is.EqualTo("entry number 040"), "the newest entries survive");
            Assert.That(restored.Select(e => e.Id), Is.Ordered.Ascending);
            for (int i = 1; i < restored.Length; i++)
            {
                Assert.That(restored[i].Id, Is.EqualTo(restored[i - 1].Id + 1), "no gaps inside the retained window");
            }
        }

        [Test]
        public void Load_TornTail_KeepsGoodRecordsAndTruncates_ThenAppendsCleanly()
        {
            using (var journal = new LogJournal(_directory))
            {
                for (int i = 1; i <= 5; i++)
                {
                    journal.Append(Sequenced(Entry("entry " + i), i));
                }
            }

            string file = Directory.GetFiles(_directory).Single();
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.SetLength(stream.Length - 10);
            }

            using (var journal = new LogJournal(_directory))
            {
                LogEntry[] restored = journal.Load(dropContexts: false).ToArray();
                Assert.That(restored.Select(e => e.Message), Is.EqualTo(new[] { "entry 1", "entry 2", "entry 3", "entry 4" }));
                journal.Append(Sequenced(Entry("entry 6"), 6));
            }

            using var reader = new LogJournal(_directory);
            Assert.That(reader.Load(dropContexts: false).Select(e => e.Message).Last(), Is.EqualTo("entry 6"));
            Assert.That(reader.Load(dropContexts: false).Count, Is.EqualTo(5));
        }

        [Test]
        public void Load_CorruptedRecordBody_StopsAtTheBadRecord()
        {
            using (var journal = new LogJournal(_directory))
            {
                journal.Append(Sequenced(Entry("good"), 1));
                journal.Append(Sequenced(Entry("flipped"), 2));
            }

            string file = Directory.GetFiles(_directory).Single();
            byte[] bytes = File.ReadAllBytes(file);
            bytes[bytes.Length - 3] ^= 0xFF;
            File.WriteAllBytes(file, bytes);

            using var journal2 = new LogJournal(_directory);
            Assert.That(journal2.Load(dropContexts: false).Select(e => e.Message), Is.EqualTo(new[] { "good" }));
        }

        [Test]
        public void Load_ForeignNewestFile_IsDiscarded_AndAppendStartsAFreshSegment()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "journal-000007.bin"), "this is not a journal");

            using (var journal = new LogJournal(_directory))
            {
                Assert.That(journal.Load(dropContexts: false), Is.Empty);
                journal.Append(Sequenced(Entry("fresh"), 1));
            }

            Assert.That(File.Exists(Path.Combine(_directory, "journal-000007.bin")), Is.False);
            Assert.That(File.Exists(Path.Combine(_directory, "journal-000008.bin")), Is.True);
            using var reader = new LogJournal(_directory);
            Assert.That(reader.Load(dropContexts: false).Single().Message, Is.EqualTo("fresh"));
        }

        [Test]
        public void Reset_DeletesEverything_AndAppendStartsOver()
        {
            using var journal = new LogJournal(_directory);
            journal.Append(Sequenced(Entry("old"), 1));
            journal.Flush();

            journal.Reset();

            Assert.That(Directory.Exists(_directory) ? Directory.GetFiles(_directory).Length : 0, Is.EqualTo(0));
            Assert.That(journal.SizeBytes, Is.EqualTo(0));
            journal.Append(Sequenced(Entry("new"), 2));
            journal.Flush();

            using var reader = new LogJournal(_directory);
            Assert.That(reader.Load(dropContexts: false).Single().Message, Is.EqualTo("new"));
        }

        [Test]
        public void Constructor_RejectsBadArguments()
        {
            Assert.Throws<ArgumentException>(() => new LogJournal(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogJournal(_directory, maxSegmentBytes: 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogJournal(_directory, maxSegments: 0));
        }

        private static LogEntry Entry(string message, ObjectRef context = default)
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, "Foo:Bar () (at Assets/Foo.cs:1)", DateTime.UtcNow, 10, 1, true, context);
        }

        private static LogEntry Sequenced(LogEntry entry, long id)
        {
            entry.AssignSequence(id, 0);
            return entry;
        }
    }
}
