using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class TimelineBucketsTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Build_SpreadsEntriesAcrossTheSpan_AndCountsBySeverity()
        {
            var entries = new List<LogEntry>
            {
                Entry(LogSeverity.Log, 0),
                Entry(LogSeverity.Warning, 1),
                Entry(LogSeverity.Error, 9),
                Entry(LogSeverity.Exception, 10),
            };

            TimelineBucket[] buckets = TimelineBuckets.Build(entries, 10, out DateTime start, out DateTime end);

            Assert.That(start, Is.EqualTo(T0));
            Assert.That(end, Is.EqualTo(T0.AddSeconds(10)));
            Assert.That(buckets.Length, Is.EqualTo(10));
            Assert.That(buckets[0].Logs, Is.EqualTo(1));
            Assert.That(buckets[1].Warnings, Is.EqualTo(1));
            Assert.That(buckets[9].Errors, Is.EqualTo(2), "the last second and the exact end both fall in the last bucket");
            Assert.That(buckets.Sum(b => b.Total), Is.EqualTo(4));
            Assert.That(TimelineBuckets.Peak(buckets), Is.EqualTo(2));
        }

        [Test]
        public void Build_RecordsTheFirstEntryOfEachBucket_ForJumping()
        {
            var entries = new List<LogEntry>
            {
                Entry(LogSeverity.Log, 0),
                Entry(LogSeverity.Log, 0.2),
                Entry(LogSeverity.Log, 5),
                Entry(LogSeverity.Log, 10),
            };

            TimelineBucket[] buckets = TimelineBuckets.Build(entries, 2, out _, out _);

            Assert.That(buckets[0].FirstIndex, Is.EqualTo(0));
            Assert.That(buckets[0].Total, Is.EqualTo(2));
            Assert.That(buckets[1].FirstIndex, Is.EqualTo(2));
            Assert.That(buckets[1].Total, Is.EqualTo(2));
        }

        [Test]
        public void Build_SkipsMarkers_ButStillUsesTheirTimeForTheSpan()
        {
            var entries = new List<LogEntry>
            {
                LogEntry.Marker("Entered Play mode", T0, 0),
                Entry(LogSeverity.Log, 4),
            };

            TimelineBucket[] buckets = TimelineBuckets.Build(entries, 4, out DateTime start, out _);

            Assert.That(start, Is.EqualTo(T0));
            Assert.That(buckets.Sum(b => b.Total), Is.EqualTo(1));
            Assert.That(buckets[0].FirstIndex, Is.EqualTo(-1), "the marker does not claim a slot");
        }

        [Test]
        public void Build_WithOneInstant_PutsEverythingInTheFirstBucket()
        {
            var entries = new List<LogEntry> { Entry(LogSeverity.Log, 0), Entry(LogSeverity.Error, 0) };

            TimelineBucket[] buckets = TimelineBuckets.Build(entries, 8, out _, out _);

            Assert.That(buckets[0].Total, Is.EqualTo(2));
            Assert.That(buckets.Skip(1).Sum(b => b.Total), Is.EqualTo(0));
        }

        [Test]
        public void Build_CopesWithOutOfOrderTimestamps()
        {
            var entries = new List<LogEntry> { Entry(LogSeverity.Log, 10), Entry(LogSeverity.Log, 0) };

            TimelineBucket[] buckets = TimelineBuckets.Build(entries, 2, out DateTime start, out DateTime end);

            Assert.That(start, Is.EqualTo(T0));
            Assert.That(end, Is.EqualTo(T0.AddSeconds(10)));
            Assert.That(buckets[0].FirstIndex, Is.EqualTo(1), "the earliest entry is not the first in the list");
            Assert.That(buckets[1].FirstIndex, Is.EqualTo(0));
        }

        [Test]
        public void Build_WithNothingToShow_ReturnsEmpty()
        {
            Assert.That(TimelineBuckets.Build(null, 10, out _, out _), Is.Empty);
            Assert.That(TimelineBuckets.Build(new List<LogEntry>(), 10, out _, out _), Is.Empty);
            Assert.That(TimelineBuckets.Build(new List<LogEntry> { Entry(LogSeverity.Log, 0) }, 0, out _, out _), Is.Empty);
            Assert.That(TimelineBuckets.Peak(Array.Empty<TimelineBucket>()), Is.EqualTo(0));
        }

        private static LogEntry Entry(LogSeverity severity, double secondsAfterStart)
        {
            return new LogEntry(LogEntryKind.Log, severity, "m", string.Empty, T0.AddSeconds(secondsAfterStart), 0, 1, true, ObjectRef.None);
        }
    }
}
