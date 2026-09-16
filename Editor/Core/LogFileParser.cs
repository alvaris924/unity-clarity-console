using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>Which shape of log file was recognised.</summary>
    internal enum LogFileFormat
    {
        /// <summary>Nothing recognisable; every line becomes its own entry.</summary>
        Unknown = 0,

        /// <summary>A Unity <c>Player.log</c> or <c>Editor.log</c>: message blocks with stack traces.</summary>
        UnityLog = 1,

        /// <summary>Android <c>logcat</c> threadtime output, which carries a level and a tag per line.</summary>
        Logcat = 2,
    }

    internal sealed class LogFileImport
    {
        public LogFileImport(LogFileFormat format, IReadOnlyList<LogEntry> entries)
        {
            Format = format;
            Entries = entries;
        }

        public LogFileFormat Format { get; }

        public IReadOnlyList<LogEntry> Entries { get; }

        public string Describe()
        {
            switch (Format)
            {
                case LogFileFormat.UnityLog:
                    return "Unity log";
                case LogFileFormat.Logcat:
                    return "Android logcat";
                default:
                    return "Plain text";
            }
        }
    }

    /// <summary>
    /// Turns a log file from a build or a device back into entries the window can show. Only text the
    /// file actually contains is used: a line without a severity stays a plain log rather than being
    /// guessed at, so an import never invents errors that were not there.
    /// </summary>
    internal static class LogFileParser
    {
        private static readonly Regex LogcatLine = new Regex(
            @"^(?<date>\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d{3})\s+\d+\s+\d+\s+(?<level>[VDIWEF])\s+(?<tag>[^:]*?)\s*:\s?(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex UnityLocationSuffix = new Regex(
            @"^\s*\(Filename:\s*(?<file>.*?)\s+Line:\s*(?<line>\d+)\s*\)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ExceptionHeader = new Regex(
            @"^(?<type>[A-Za-z_][\w.`+]*Exception)\s*:",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Files larger than this are refused rather than pulled into memory.</summary>
        public const long MaxBytes = 64 * 1024 * 1024;

        public static LogFileImport Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new LogFileImport(LogFileFormat.Unknown, Array.Empty<LogEntry>());
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            LogFileFormat format = Detect(lines);

            List<LogEntry> entries = format == LogFileFormat.Logcat ? ParseLogcat(lines) : ParseUnityLog(lines);
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].AssignSequence(i + 1, 0);
            }

            return new LogFileImport(format, entries);
        }

        /// <summary>Looks at the first lines only: enough to tell the formats apart, cheap on a huge file.</summary>
        internal static LogFileFormat Detect(IReadOnlyList<string> lines)
        {
            int inspected = 0;
            for (int i = 0; i < lines.Count && inspected < 40; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                inspected++;
                if (LogcatLine.IsMatch(line))
                {
                    return LogFileFormat.Logcat;
                }

                if (UnityLocationSuffix.IsMatch(line) || line.Contains("(at Assets/") || line.StartsWith("Mono path[0]", StringComparison.Ordinal))
                {
                    return LogFileFormat.UnityLog;
                }
            }

            return inspected == 0 ? LogFileFormat.Unknown : LogFileFormat.UnityLog;
        }

        private static List<LogEntry> ParseLogcat(IReadOnlyList<string> lines)
        {
            var entries = new List<LogEntry>();
            int year = DateTime.Now.Year;

            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                Match match = LogcatLine.Match(line);
                if (!match.Success)
                {
                    // A continuation line belongs to the entry above it, usually a stack frame.
                    Append(entries, line);
                    continue;
                }

                string tag = match.Groups["tag"].Value.Trim();
                string message = match.Groups["message"].Value;
                entries.Add(new LogEntry(
                    LogEntryKind.Log,
                    SeverityFromLogcat(match.Groups["level"].Value),
                    tag.Length > 0 ? "[" + tag + "] " + message : message,
                    string.Empty,
                    ParseLogcatTime(year, match.Groups["date"].Value, match.Groups["time"].Value),
                    0,
                    0,
                    true,
                    ObjectRef.None));
            }

            return entries;
        }

        private static List<LogEntry> ParseUnityLog(IReadOnlyList<string> lines)
        {
            var entries = new List<LogEntry>();
            var message = new StringBuilder();
            var stack = new StringBuilder();
            LogSeverity severity = LogSeverity.Log;
            bool inStack = false;

            void Flush()
            {
                if (message.Length == 0 && stack.Length == 0)
                {
                    return;
                }

                entries.Add(new LogEntry(
                    LogEntryKind.Log,
                    severity,
                    message.ToString().TrimEnd(),
                    stack.ToString().TrimEnd(),
                    DateTime.UtcNow,
                    0,
                    0,
                    true,
                    ObjectRef.None));

                message.Clear();
                stack.Clear();
                severity = LogSeverity.Log;
                inStack = false;
            }

            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    Flush();
                    continue;
                }

                Match location = UnityLocationSuffix.Match(line);
                if (location.Success)
                {
                    // Unity appends the call site after the stack. Rewrite it into the shape the stack
                    // parser understands, so an imported entry is just as clickable as a captured one.
                    stack.Append("(at ").Append(location.Groups["file"].Value).Append(':').Append(location.Groups["line"].Value).Append(")\n");
                    continue;
                }

                if (message.Length == 0)
                {
                    severity = SeverityFromUnityLine(line);
                    message.Append(line);
                    continue;
                }

                if (!inStack && LooksLikeFrame(line))
                {
                    inStack = true;
                }

                if (inStack)
                {
                    stack.Append(line).Append('\n');
                }
                else
                {
                    message.Append('\n').Append(line);
                }
            }

            Flush();
            return entries;
        }

        private static void Append(List<LogEntry> entries, string line)
        {
            if (entries.Count == 0)
            {
                return;
            }

            LogEntry last = entries[entries.Count - 1];
            entries[entries.Count - 1] = new LogEntry(
                last.Kind,
                last.Severity,
                last.Message,
                last.StackTrace.Length == 0 ? line.Trim() : last.StackTrace + "\n" + line.Trim(),
                last.TimestampUtc,
                last.Frame,
                last.ThreadId,
                last.IsMainThread,
                last.Context);
        }

        private static bool LooksLikeFrame(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("at ", StringComparison.Ordinal)
                || trimmed.Contains("(at ")
                || (line.StartsWith("  ", StringComparison.Ordinal) && trimmed.Contains(":"));
        }

        /// <summary>
        /// Only the markers Unity itself writes are honoured. Anything else stays a plain log, so an
        /// import does not invent severities the file never claimed.
        /// </summary>
        internal static LogSeverity SeverityFromUnityLine(string line)
        {
            if (ExceptionHeader.IsMatch(line))
            {
                return LogSeverity.Exception;
            }

            if (line.StartsWith("Assertion failed", StringComparison.OrdinalIgnoreCase))
            {
                return LogSeverity.Assert;
            }

            if (line.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || line.Contains("error CS"))
            {
                return LogSeverity.Error;
            }

            if (line.StartsWith("Warning", StringComparison.OrdinalIgnoreCase) || line.Contains("warning CS"))
            {
                return LogSeverity.Warning;
            }

            return LogSeverity.Log;
        }

        internal static LogSeverity SeverityFromLogcat(string level)
        {
            switch (level)
            {
                case "E":
                case "F":
                    return LogSeverity.Error;
                case "W":
                    return LogSeverity.Warning;
                default:
                    return LogSeverity.Log;
            }
        }

        private static DateTime ParseLogcatTime(int year, string date, string time)
        {
            // logcat omits the year, so the current one is the best available guess.
            string stamp = year.ToString(CultureInfo.InvariantCulture) + "-" + date + " " + time;
            return DateTime.TryParseExact(stamp, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)
                ? parsed
                : DateTime.UtcNow;
        }
    }
}
