using System;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>A message and how often it was logged, with the first entry that carried it.</summary>
    internal sealed class ReportLine
    {
        public ReportLine(string text, int count, LogEntry first)
        {
            Text = text;
            Count = count;
            First = first;
        }

        /// <summary>The message, or the channel name for a channel line.</summary>
        public string Text { get; }

        public int Count { get; }

        /// <summary>The first entry of the run with this message or in this channel.</summary>
        public LogEntry First { get; }
    }

    /// <summary>
    /// A one-screen summary of one Play session: how long it ran, what it logged by severity, the first
    /// error, which usually is the root cause of the ones after it, and the messages and channels that
    /// logged the most. Built from the entries the store still holds, so a session partly evicted from
    /// the ring buffer says so rather than pretending to be complete.
    /// </summary>
    internal sealed class SessionReport
    {
        /// <summary>How many lines each top list keeps.</summary>
        public const int TopCount = 5;

        private SessionReport(int session)
        {
            Session = session;
        }

        public int Session { get; }

        /// <summary>False when no entry of the session is left in the store.</summary>
        public bool HasEntries { get; private set; }

        /// <summary>True when the "Entered Play mode" divider is gone, so older entries were evicted.</summary>
        public bool IsTruncated { get; private set; }

        /// <summary>True once the "Exiting Play mode" divider was seen; false while the session still runs.</summary>
        public bool HasEnded { get; private set; }

        public DateTime StartUtc { get; private set; }

        public DateTime EndUtc { get; private set; }

        public TimeSpan Duration => EndUtc - StartUtc;

        /// <summary>Engine frames between the first and the last entry of the run.</summary>
        public int FramesSpanned { get; private set; }

        public int Logs { get; private set; }

        public int Warnings { get; private set; }

        /// <summary>Errors, exceptions and assertions together.</summary>
        public int Errors { get; private set; }

        /// <summary>How many different error messages there were; many errors are often one repeated.</summary>
        public int DistinctErrors { get; private set; }

        /// <summary>The first error, exception or assertion of the run, or null for a clean run.</summary>
        public LogEntry FirstError { get; private set; }

        /// <summary>The most repeated error messages, most frequent first.</summary>
        public IReadOnlyList<ReportLine> TopErrors { get; private set; } = Array.Empty<ReportLine>();

        /// <summary>The most repeated messages of any severity: where the log spam comes from.</summary>
        public IReadOnlyList<ReportLine> TopMessages { get; private set; } = Array.Empty<ReportLine>();

        /// <summary>The channels with the most entries.</summary>
        public IReadOnlyList<ReportLine> TopChannels { get; private set; } = Array.Empty<ReportLine>();

        /// <summary>Average frames per second over the run, or 0 when it cannot be told.</summary>
        public double FramesPerSecond => Duration.TotalSeconds > 0.5 && FramesSpanned > 0 ? FramesSpanned / Duration.TotalSeconds : 0;

        /// <summary>One line that says how the run went, for the top of the report.</summary>
        public string Verdict
        {
            get
            {
                if (!HasEntries)
                {
                    return "Nothing is left of session " + Session + " in the console.";
                }

                if (Errors > 0)
                {
                    string errors = Errors == 1 ? "1 error" : Errors + " errors";
                    string distinct = DistinctErrors > 1 && DistinctErrors < Errors ? ", " + DistinctErrors + " different" : string.Empty;
                    return errors + distinct + ". The first came " + FormatOffset(FirstError.TimestampUtc - StartUtc) + " in.";
                }

                if (Warnings > 0)
                {
                    return "No errors; " + (Warnings == 1 ? "1 warning." : Warnings + " warnings.");
                }

                return "A clean run: no errors or warnings.";
            }
        }

        /// <summary>The Play sessions the entries belong to, newest first.</summary>
        public static List<int> SessionsIn(IEnumerable<LogEntry> entries)
        {
            var sessions = new List<int>();
            var seen = new HashSet<int>();
            foreach (LogEntry entry in entries)
            {
                if (entry.Session > 0 && seen.Add(entry.Session))
                {
                    sessions.Add(entry.Session);
                }
            }

            sessions.Sort((a, b) => b.CompareTo(a));
            return sessions;
        }

        /// <summary>
        /// Summarises session <paramref name="session"/>. Entries of that session logged after its
        /// "Exiting Play mode" divider, in Edit mode, are not part of the run and are left out.
        /// </summary>
        public static SessionReport Build(IEnumerable<LogEntry> entries, int session)
        {
            var report = new SessionReport(session) { IsTruncated = true };
            var errorCounts = new Dictionary<string, Tally>();
            var messageCounts = new Dictionary<string, Tally>();
            var channelCounts = new Dictionary<string, Tally>();
            int firstFrame = 0;
            int lastFrame = 0;

            foreach (LogEntry entry in entries)
            {
                if (entry.Session != session)
                {
                    continue;
                }

                if (entry.Kind == LogEntryKind.Marker)
                {
                    if (entry.Message.StartsWith(LogEntry.EnteredPlayModeMarker, StringComparison.Ordinal))
                    {
                        report.IsTruncated = false;
                        Stretch(report, entry, ref firstFrame, ref lastFrame);
                    }
                    else if (entry.Message == LogEntry.ExitingPlayModeMarker)
                    {
                        report.HasEnded = true;
                        Stretch(report, entry, ref firstFrame, ref lastFrame);
                        break;
                    }

                    continue;
                }

                Stretch(report, entry, ref firstFrame, ref lastFrame);
                switch (entry.Severity)
                {
                    case LogSeverity.Log:
                        report.Logs++;
                        break;
                    case LogSeverity.Warning:
                        report.Warnings++;
                        break;
                    default:
                        report.Errors++;
                        report.FirstError = report.FirstError ?? entry;
                        Count(errorCounts, entry.Message, entry);
                        break;
                }

                Count(messageCounts, entry.Message, entry);
                if (entry.Channel.Length > 0)
                {
                    Count(channelCounts, entry.Channel, entry);
                }
            }

            report.FramesSpanned = Math.Max(0, lastFrame - firstFrame);
            report.DistinctErrors = errorCounts.Count;
            report.TopErrors = Top(errorCounts, 1);
            report.TopMessages = Top(messageCounts, 2);
            report.TopChannels = Top(channelCounts, 1);
            return report;
        }

        /// <summary>"0:41" or "1:02:05" for a time into the run.</summary>
        public static string FormatOffset(TimeSpan offset)
        {
            if (offset < TimeSpan.Zero)
            {
                offset = TimeSpan.Zero;
            }

            return offset.TotalHours >= 1
                ? ((int)offset.TotalHours) + ":" + offset.Minutes.ToString("00") + ":" + offset.Seconds.ToString("00")
                : ((int)offset.TotalMinutes) + ":" + offset.Seconds.ToString("00");
        }

        private static void Stretch(SessionReport report, LogEntry entry, ref int firstFrame, ref int lastFrame)
        {
            if (!report.HasEntries)
            {
                report.HasEntries = true;
                report.StartUtc = entry.TimestampUtc;
                firstFrame = entry.Frame;
            }

            report.EndUtc = entry.TimestampUtc;
            lastFrame = entry.Frame;
        }

        private static void Count(Dictionary<string, Tally> counts, string key, LogEntry entry)
        {
            if (counts.TryGetValue(key, out Tally tally))
            {
                tally.Count++;
            }
            else
            {
                counts[key] = new Tally { Count = 1, First = entry, Order = counts.Count };
            }
        }

        /// <summary>The <see cref="TopCount"/> most frequent keys seen at least <paramref name="minimum"/> times; ties keep first-seen order.</summary>
        private static IReadOnlyList<ReportLine> Top(Dictionary<string, Tally> counts, int minimum)
        {
            var ranked = new List<KeyValuePair<string, Tally>>();
            foreach (KeyValuePair<string, Tally> pair in counts)
            {
                if (pair.Value.Count >= minimum)
                {
                    ranked.Add(pair);
                }
            }

            ranked.Sort((a, b) =>
            {
                int byCount = b.Value.Count.CompareTo(a.Value.Count);
                return byCount != 0 ? byCount : a.Value.Order.CompareTo(b.Value.Order);
            });

            var lines = new List<ReportLine>(Math.Min(TopCount, ranked.Count));
            for (int i = 0; i < ranked.Count && i < TopCount; i++)
            {
                lines.Add(new ReportLine(ranked[i].Key, ranked[i].Value.Count, ranked[i].Value.First));
            }

            return lines;
        }

        private sealed class Tally
        {
            public int Count;
            public LogEntry First;
            public int Order;
        }
    }
}
