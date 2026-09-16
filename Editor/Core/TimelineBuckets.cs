using System;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>Counts for one slice of time on the timeline strip.</summary>
    internal struct TimelineBucket
    {
        public int Logs;
        public int Warnings;
        public int Errors;

        /// <summary>Index into the source list of the first entry in this bucket, or -1 when empty.</summary>
        public int FirstIndex;

        public int Total => Logs + Warnings + Errors;
    }

    /// <summary>
    /// Folds a list of entries into a fixed number of time buckets so the strip can be drawn in one pass
    /// with no per-entry allocation. Markers are skipped; they have no severity to count.
    /// </summary>
    internal static class TimelineBuckets
    {
        /// <summary>
        /// Buckets <paramref name="entries"/> between the timestamps of its first and last entries.
        /// Returns an empty array when there is nothing to show.
        /// </summary>
        public static TimelineBucket[] Build(IReadOnlyList<LogEntry> entries, int bucketCount, out DateTime start, out DateTime end)
        {
            start = default;
            end = default;

            if (entries == null || entries.Count == 0 || bucketCount <= 0)
            {
                return Array.Empty<TimelineBucket>();
            }

            start = entries[0].TimestampUtc;
            end = entries[entries.Count - 1].TimestampUtc;
            for (int i = 0; i < entries.Count; i++)
            {
                DateTime at = entries[i].TimestampUtc;
                if (at < start)
                {
                    start = at;
                }

                if (at > end)
                {
                    end = at;
                }
            }

            var buckets = new TimelineBucket[bucketCount];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i].FirstIndex = -1;
            }

            double span = (end - start).TotalSeconds;
            for (int i = 0; i < entries.Count; i++)
            {
                LogEntry entry = entries[i];
                if (entry.Kind != LogEntryKind.Log)
                {
                    continue;
                }

                int bucket = span <= 0
                    ? 0
                    : Math.Min(bucketCount - 1, (int)((entry.TimestampUtc - start).TotalSeconds / span * bucketCount));

                if (buckets[bucket].FirstIndex < 0)
                {
                    buckets[bucket].FirstIndex = i;
                }

                switch (entry.Severity)
                {
                    case LogSeverity.Warning:
                        buckets[bucket].Warnings++;
                        break;
                    case LogSeverity.Error:
                    case LogSeverity.Exception:
                    case LogSeverity.Assert:
                        buckets[bucket].Errors++;
                        break;
                    default:
                        buckets[bucket].Logs++;
                        break;
                }
            }

            return buckets;
        }

        /// <summary>The largest bucket total, used to scale the bars. Zero when everything is empty.</summary>
        public static int Peak(TimelineBucket[] buckets)
        {
            int peak = 0;
            for (int i = 0; i < buckets.Length; i++)
            {
                peak = Math.Max(peak, buckets[i].Total);
            }

            return peak;
        }
    }
}
