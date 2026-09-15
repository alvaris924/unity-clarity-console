using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClarityConsole.Core
{
    internal enum ExportFormat
    {
        /// <summary>One entry per block, the way a console reads. Best for pasting into a chat or an issue.</summary>
        Text = 0,

        /// <summary>Fenced blocks and headings, for issue trackers that render Markdown.</summary>
        Markdown = 1,

        /// <summary>An array of objects, for anything that wants to parse the entries back.</summary>
        Json = 2,
    }

    internal sealed class ExportOptions
    {
        public ExportFormat Format { get; set; } = ExportFormat.Text;

        /// <summary>Include each entry's stack trace. Off makes a short, readable list.</summary>
        public bool IncludeStackTraces { get; set; } = true;

        /// <summary>
        /// Lines describing where the export came from: Unity version, platform, the active filters. The
        /// Core layer cannot read those itself, so the caller supplies them.
        /// </summary>
        public IReadOnlyList<string> Header { get; set; }
    }

    /// <summary>
    /// Turns entries into text a person or a tool can read. Deliberately hand-written rather than pulling
    /// in a serializer: the package ships no third-party dependencies.
    /// </summary>
    internal static class LogExporter
    {
        public static string Export(IEnumerable<LogEntry> entries, ExportOptions options)
        {
            options = options ?? new ExportOptions();
            var builder = new StringBuilder();

            switch (options.Format)
            {
                case ExportFormat.Markdown:
                    WriteMarkdown(builder, entries, options);
                    break;
                case ExportFormat.Json:
                    WriteJson(builder, entries, options);
                    break;
                default:
                    WriteText(builder, entries, options);
                    break;
            }

            return builder.ToString();
        }

        /// <summary>The file extension an export of this format should use.</summary>
        public static string ExtensionFor(ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Markdown:
                    return "md";
                case ExportFormat.Json:
                    return "json";
                default:
                    return "txt";
            }
        }

        private static void WriteText(StringBuilder builder, IEnumerable<LogEntry> entries, ExportOptions options)
        {
            if (options.Header != null)
            {
                foreach (string line in options.Header)
                {
                    builder.Append("# ").AppendLine(line);
                }

                builder.AppendLine();
            }

            foreach (LogEntry entry in Safe(entries))
            {
                builder.Append('[').Append(Timestamp(entry)).Append("] ")
                    .Append(Label(entry))
                    .Append(entry.Channel.Length > 0 ? " [" + entry.Channel + "]" : string.Empty)
                    .Append(' ')
                    .AppendLine(entry.Message);

                if (options.IncludeStackTraces && entry.StackTrace.Length > 0)
                {
                    foreach (string line in Lines(entry.StackTrace))
                    {
                        builder.Append("    ").AppendLine(line);
                    }
                }
            }
        }

        private static void WriteMarkdown(StringBuilder builder, IEnumerable<LogEntry> entries, ExportOptions options)
        {
            builder.AppendLine("# Console export").AppendLine();

            if (options.Header != null)
            {
                foreach (string line in options.Header)
                {
                    builder.Append("- ").AppendLine(line);
                }

                builder.AppendLine();
            }

            foreach (LogEntry entry in Safe(entries))
            {
                builder.Append("### ").Append(Label(entry)).Append(' ').AppendLine(FirstLine(entry.Message));
                builder.Append('`').Append(Timestamp(entry)).Append('`');
                if (entry.Channel.Length > 0)
                {
                    builder.Append("  ·  `").Append(entry.Channel).Append('`');
                }

                builder.AppendLine().AppendLine();

                if (entry.Message.IndexOf('\n') >= 0 || (options.IncludeStackTraces && entry.StackTrace.Length > 0))
                {
                    builder.AppendLine("```");
                    builder.AppendLine(entry.Message.TrimEnd());
                    if (options.IncludeStackTraces && entry.StackTrace.Length > 0)
                    {
                        builder.AppendLine();
                        builder.AppendLine(entry.StackTrace.TrimEnd());
                    }

                    builder.AppendLine("```").AppendLine();
                }
            }
        }

        private static void WriteJson(StringBuilder builder, IEnumerable<LogEntry> entries, ExportOptions options)
        {
            builder.AppendLine("{");

            if (options.Header != null)
            {
                builder.Append("  \"header\": [");
                bool firstLine = true;
                foreach (string line in options.Header)
                {
                    builder.Append(firstLine ? string.Empty : ", ").Append(Quote(line));
                    firstLine = false;
                }

                builder.AppendLine("],");
            }

            builder.AppendLine("  \"entries\": [");
            bool first = true;
            foreach (LogEntry entry in Safe(entries))
            {
                if (!first)
                {
                    builder.AppendLine(",");
                }

                first = false;
                builder.Append("    {")
                    .Append("\"id\": ").Append(entry.Id.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"time\": ").Append(Quote(entry.TimestampUtc.ToString("o", CultureInfo.InvariantCulture)))
                    .Append(", \"kind\": ").Append(Quote(entry.Kind.ToString()))
                    .Append(", \"severity\": ").Append(Quote(entry.Severity.ToString()))
                    .Append(", \"frame\": ").Append(entry.Frame.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"session\": ").Append(entry.Session.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"channel\": ").Append(Quote(entry.Channel))
                    .Append(", \"message\": ").Append(Quote(entry.Message));

                if (options.IncludeStackTraces)
                {
                    builder.Append(", \"stackTrace\": ").Append(Quote(entry.StackTrace));
                }

                builder.Append('}');
            }

            if (!first)
            {
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.Append('}');
        }

        private static IEnumerable<LogEntry> Safe(IEnumerable<LogEntry> entries)
        {
            return entries ?? Array.Empty<LogEntry>();
        }

        private static string Timestamp(LogEntry entry)
        {
            return entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private static string Label(LogEntry entry)
        {
            return entry.Kind == LogEntryKind.Marker ? "MARKER" : entry.Severity.ToString().ToUpperInvariant();
        }

        private static string FirstLine(string message)
        {
            int newline = message.IndexOf('\n');
            return newline < 0 ? message : message.Substring(0, newline).TrimEnd('\r');
        }

        private static IEnumerable<string> Lines(string text)
        {
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        /// <summary>Minimal JSON string escaping, enough for messages and stack traces.</summary>
        internal static string Quote(string value)
        {
            var builder = new StringBuilder(value == null ? 2 : value.Length + 2);
            builder.Append('"');

            if (value != null)
            {
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (c < ' ')
                            {
                                builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(c);
                            }

                            break;
                    }
                }
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
