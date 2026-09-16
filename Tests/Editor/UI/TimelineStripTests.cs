using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class TimelineStripTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Refresh_BuildsBuckets_AndShowsTheStrip()
        {
            var strip = new TimelineStrip { BucketCount = 4 };

            strip.Refresh(new List<LogEntry> { Entry(0), Entry(3) });

            Assert.That(strip.Buckets.Length, Is.EqualTo(4));
            Assert.That(strip.Buckets[0].Total, Is.EqualTo(1));
            Assert.That(strip.Buckets[3].Total, Is.EqualTo(1));
            Assert.That(strip.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(strip.tooltip, Does.Contain("Click a bar"));
        }

        [Test]
        public void Refresh_WithNothing_HidesTheStrip()
        {
            var strip = new TimelineStrip();
            strip.Refresh(new List<LogEntry> { Entry(0) });

            strip.Refresh(new List<LogEntry>());

            Assert.That(strip.Buckets, Is.Empty);
            Assert.That(strip.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Refresh_WithOnlyMarkers_HidesTheStrip()
        {
            var strip = new TimelineStrip();

            strip.Refresh(new List<LogEntry> { LogEntry.Marker("Domain reloaded", T0, 0) });

            Assert.That(strip.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void SliceAt_WithoutALayout_IsOutOfRange()
        {
            var strip = new TimelineStrip();
            strip.Refresh(new List<LogEntry> { Entry(0) });

            // No panel means no resolved width, so nothing can be hit.
            Assert.That(strip.SliceAt(10f), Is.EqualTo(-1));
        }

        private static LogEntry Entry(double secondsAfterStart)
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, "m", string.Empty, T0.AddSeconds(secondsAfterStart), 0, 1, true, ObjectRef.None);
        }
    }
}
