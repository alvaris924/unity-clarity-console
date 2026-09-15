using System;

namespace ClarityConsole.Core
{
    /// <summary>
    /// One captured line: a log message or a session marker. Immutable after construction except for the
    /// sequence data the store stamps on append.
    /// </summary>
    internal sealed class LogEntry
    {
        private ParsedTrace _trace;

        public LogEntry(
            LogEntryKind kind,
            LogSeverity severity,
            string message,
            string stackTrace,
            DateTime timestampUtc,
            int frame,
            int threadId,
            bool isMainThread,
            ObjectRef context)
        {
            Kind = kind;
            Severity = severity;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            TimestampUtc = timestampUtc;
            Frame = frame;
            ThreadId = threadId;
            IsMainThread = isMainThread;
            Context = context;
        }

        /// <summary>Sequence number assigned by the store; 0 until appended. Unique for the Editor session.</summary>
        public long Id { get; private set; }

        public LogEntryKind Kind { get; }

        public LogSeverity Severity { get; }

        public string Message { get; }

        /// <summary>Raw stack trace text as Unity delivered it; empty when traces are disabled for the severity.</summary>
        public string StackTrace { get; }

        public DateTime TimestampUtc { get; }

        /// <summary>Frame counter at capture time: the engine frame in Play mode, an Editor tick otherwise.</summary>
        public int Frame { get; }

        public int ThreadId { get; }

        public bool IsMainThread { get; }

        public ObjectRef Context { get; }

        /// <summary>Play session the entry belongs to; 0 before the first Play in this Editor run.</summary>
        public int Session { get; private set; }

        /// <summary>The stack trace split into frames. Parsed on first access and cached; never on the capture path.</summary>
        public ParsedTrace Trace => _trace ?? (_trace = StackTraceParser.Parse(StackTrace));

        public static LogEntry Marker(string message, DateTime timestampUtc, int frame)
        {
            return new LogEntry(LogEntryKind.Marker, LogSeverity.Log, message, string.Empty, timestampUtc, frame, 0, true, ObjectRef.None);
        }

        internal void AssignSequence(long id, int session)
        {
            Id = id;
            Session = session;
        }
    }
}
