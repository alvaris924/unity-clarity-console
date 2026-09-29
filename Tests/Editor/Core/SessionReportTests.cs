using System;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class SessionReportTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Build_SummarisesOneRun_BetweenItsPlayModeMarkers()
        {
            LogStore store = StoreWithTwoRuns();

            SessionReport report = SessionReport.Build(store.Entries, 2);

            Assert.That(report.HasEntries, Is.True);
            Assert.That(report.HasEnded, Is.True);
            Assert.That(report.IsTruncated, Is.False);
            Assert.That(report.Duration, Is.EqualTo(TimeSpan.FromSeconds(100)));
            Assert.That(report.FramesSpanned, Is.EqualTo(6000));
            Assert.That(report.FramesPerSecond, Is.EqualTo(60).Within(0.01));
            Assert.That(report.Logs, Is.EqualTo(4), "the Edit mode log after the run is not part of it");
            Assert.That(report.Warnings, Is.EqualTo(1));
            Assert.That(report.Errors, Is.EqualTo(3), "errors, exceptions and assertions count together");
            Assert.That(report.DistinctErrors, Is.EqualTo(2));
            Assert.That(report.FirstError.Message, Is.EqualTo("[Combat] target missing"));
        }

        [Test]
        public void Build_RanksRepeats_AndKeepsOnlyRepeatedMessagesAsChatty()
        {
            SessionReport report = SessionReport.Build(StoreWithTwoRuns().Entries, 2);

            Assert.That(report.TopErrors.Select(l => l.Text + "×" + l.Count), Is.EqualTo(new[] { "[Combat] target missing×2", "NullReferenceException: boom×1" }));
            Assert.That(report.TopMessages.Select(l => l.Text + "×" + l.Count), Is.EqualTo(new[] { "[Net] tick×3", "[Combat] target missing×2" }), "a message logged once is not spam");
            Assert.That(report.TopChannels.Select(l => l.Text + "×" + l.Count), Is.EqualTo(new[] { "Net×3", "Combat×2" }));
            Assert.That(report.TopMessages[0].First.Frame, Is.EqualTo(1010), "each line points at its first entry");
        }

        [Test]
        public void Verdict_ReadsLikeASentence()
        {
            SessionReport withErrors = SessionReport.Build(StoreWithTwoRuns().Entries, 2);
            Assert.That(withErrors.Verdict, Is.EqualTo("3 errors, 2 different. The first came 0:20 in."));

            SessionReport clean = SessionReport.Build(StoreWithTwoRuns().Entries, 1);
            Assert.That(clean.Verdict, Is.EqualTo("A clean run: no errors or warnings."));

            SessionReport missing = SessionReport.Build(StoreWithTwoRuns().Entries, 7);
            Assert.That(missing.HasEntries, Is.False);
            Assert.That(missing.Verdict, Does.Contain("session 7"));
        }

        [Test]
        public void Build_ARunStillGoing_OrWhoseStartWasEvicted_SaysSo()
        {
            var store = new LogStore();
            store.CurrentSession = 1;
            store.Append(Log(LogSeverity.Warning, "late warning", 5, 300));

            SessionReport report = SessionReport.Build(store.Entries, 1);

            Assert.That(report.IsTruncated, Is.True, "no Entered Play mode marker was left");
            Assert.That(report.HasEnded, Is.False);
            Assert.That(report.Verdict, Is.EqualTo("No errors; 1 warning."));
        }

        [Test]
        public void SessionsIn_ListsPlaySessions_NewestFirst_WithoutEditModeBeforeThem()
        {
            Assert.That(SessionReport.SessionsIn(StoreWithTwoRuns().Entries), Is.EqualTo(new[] { 2, 1 }));
        }

        [TestCase(0, "0:00")]
        [TestCase(41, "0:41")]
        [TestCase(125, "2:05")]
        [TestCase(3725, "1:02:05")]
        [TestCase(-3, "0:00")]
        public void FormatOffset_ReadsAsAClock(int seconds, string expected)
        {
            Assert.That(SessionReport.FormatOffset(TimeSpan.FromSeconds(seconds)), Is.EqualTo(expected));
        }

        /// <summary>
        /// An Edit mode log, a clean run as session 1, then session 2: 100 seconds, 6000 frames, repeated
        /// network ticks and combat errors, one exception, one warning, and an Edit mode log after it ends.
        /// </summary>
        private static LogStore StoreWithTwoRuns()
        {
            var store = new LogStore();
            store.ChannelExtractor = new ChannelExtractor();
            store.Append(Log(LogSeverity.Log, "before any Play", 0, 0));

            store.CurrentSession = 1;
            store.Append(LogEntry.Marker(LogEntry.EnteredPlayModeMarker + "1", Start, 10));
            store.Append(Log(LogSeverity.Log, "hello", 1, 20));
            store.Append(LogEntry.Marker(LogEntry.ExitingPlayModeMarker, Start.AddSeconds(2), 30));

            store.CurrentSession = 2;
            DateTime run = Start.AddMinutes(10);
            store.Append(LogEntry.Marker(LogEntry.EnteredPlayModeMarker + "2", run, 1000));
            store.Append(Log(LogSeverity.Log, "[Net] tick", 610, 1010));
            store.Append(Log(LogSeverity.Log, "[Net] tick", 615, 1300));
            store.Append(Log(LogSeverity.Error, "[Combat] target missing", 620, 2200));
            store.Append(Log(LogSeverity.Warning, "slow frame", 630, 2800));
            store.Append(Log(LogSeverity.Exception, "NullReferenceException: boom", 640, 3400));
            store.Append(Log(LogSeverity.Error, "[Combat] target missing", 650, 4000));
            store.Append(Log(LogSeverity.Log, "[Net] tick", 660, 5000));
            store.Append(Log(LogSeverity.Log, "saved", 690, 6900));
            store.Append(LogEntry.Marker(LogEntry.ExitingPlayModeMarker, run.AddSeconds(100), 7000));
            store.Append(Log(LogSeverity.Log, "Edit mode again", 720, 7000));
            return store;
        }

        private static LogEntry Log(LogSeverity severity, string message, int seconds, int frame)
        {
            return new LogEntry(LogEntryKind.Log, severity, message, string.Empty, Start.AddSeconds(seconds), frame, 1, true, ObjectRef.None);
        }
    }
}
