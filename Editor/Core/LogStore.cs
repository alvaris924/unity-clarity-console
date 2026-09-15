using System;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Main-thread owner of captured entries. Assigns sequence numbers, keeps per-severity counts in step
    /// with the ring buffer's evictions, and notifies listeners. Not thread-safe by design.
    /// </summary>
    internal sealed class LogStore
    {
        public const int DefaultCapacity = 100_000;

        private static readonly int SeverityCount = Enum.GetValues(typeof(LogSeverity)).Length;

        private readonly RingBuffer<LogEntry> _entries;
        private readonly int[] _severityCounts = new int[SeverityCount];
        private long _nextId = 1;

        public LogStore(int capacity = DefaultCapacity)
        {
            _entries = new RingBuffer<LogEntry>(capacity);
        }

        public event Action<LogEntry> EntryAppended;

        public event Action Cleared;

        public int Count => _entries.Count;

        public int Capacity => _entries.Capacity;

        public long Appended => _entries.Appended;

        public long Overwritten => _entries.Overwritten;

        /// <summary>Play session stamped on new entries. The capture layer advances it.</summary>
        public int CurrentSession { get; set; }

        public IEnumerable<LogEntry> Entries => _entries;

        public LogEntry this[int index] => _entries[index];

        /// <summary>Number of retained log entries with the given severity. Markers are not counted.</summary>
        public int CountOf(LogSeverity severity) => _severityCounts[(int)severity];

        public void Append(LogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            entry.AssignSequence(_nextId++, CurrentSession);

            if (_entries.Append(entry, out LogEntry evicted) && evicted.Kind == LogEntryKind.Log)
            {
                _severityCounts[(int)evicted.Severity]--;
            }

            if (entry.Kind == LogEntryKind.Log)
            {
                _severityCounts[(int)entry.Severity]++;
            }

            EntryAppended?.Invoke(entry);
        }

        /// <summary>Drops every retained entry. Sequence numbers keep counting so ids stay unique.</summary>
        public void Clear()
        {
            _entries.Clear();
            Array.Clear(_severityCounts, 0, _severityCounts.Length);
            Cleared?.Invoke();
        }
    }
}
