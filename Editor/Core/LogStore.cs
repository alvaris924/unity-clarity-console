using System;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Main-thread owner of captured entries. Assigns sequence numbers and channels, keeps per-severity
    /// and per-channel counts in step with the ring buffer's evictions, and notifies listeners.
    /// Not thread-safe by design.
    /// </summary>
    internal sealed class LogStore
    {
        public const int DefaultCapacity = 100_000;

        private static readonly int SeverityCount = Enum.GetValues(typeof(LogSeverity)).Length;

        private readonly RingBuffer<LogEntry> _entries;
        private readonly int[] _severityCounts = new int[SeverityCount];
        private readonly Dictionary<string, int> _channelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private long _nextId = 1;

        public LogStore(int capacity = DefaultCapacity)
        {
            _entries = new RingBuffer<LogEntry>(capacity);
        }

        public event Action<LogEntry> EntryAppended;

        /// <summary>Raised when the buffer overwrote its oldest entry to make room for a new one.</summary>
        public event Action<LogEntry> EntryEvicted;

        public event Action Cleared;

        /// <summary>Raised after <see cref="ReassignChannels"/> rewrote every entry's channel.</summary>
        public event Action ChannelsReassigned;

        public int Count => _entries.Count;

        public int Capacity => _entries.Capacity;

        public long Appended => _entries.Appended;

        public long Overwritten => _entries.Overwritten;

        /// <summary>Play session stamped on new entries. The capture layer advances it.</summary>
        public int CurrentSession { get; set; }

        /// <summary>Assigns <see cref="LogEntry.Channel"/> on insert. Null leaves every channel empty.</summary>
        public ChannelExtractor ChannelExtractor { get; set; }

        public IEnumerable<LogEntry> Entries => _entries;

        /// <summary>Retained log entries per channel, empty channels excluded. Live view, do not mutate.</summary>
        public IEnumerable<KeyValuePair<string, int>> ChannelCounts => _channelCounts;

        public LogEntry this[int index] => _entries[index];

        /// <summary>Number of retained log entries with the given severity. Markers are not counted.</summary>
        public int CountOf(LogSeverity severity) => _severityCounts[(int)severity];

        public int CountOfChannel(string channel)
        {
            return channel != null && _channelCounts.TryGetValue(channel, out int count) ? count : 0;
        }

        public void Append(LogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            entry.AssignSequence(_nextId++, CurrentSession);
            Insert(entry);
            EntryAppended?.Invoke(entry);
        }

        /// <summary>
        /// Re-inserts an entry read back from the journal, keeping its id and session. Raises no append
        /// event, so a journal attached afterwards never writes restored entries twice. The id sequence
        /// and the current session continue after the highest values restored.
        /// </summary>
        public void Restore(LogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            Insert(entry);
            if (entry.Id >= _nextId)
            {
                _nextId = entry.Id + 1;
            }

            if (entry.Session > CurrentSession)
            {
                CurrentSession = entry.Session;
            }
        }

        /// <summary>Swaps the extractor and recomputes every retained entry's channel.</summary>
        public void ReassignChannels(ChannelExtractor extractor)
        {
            ChannelExtractor = extractor;
            _channelCounts.Clear();

            foreach (LogEntry entry in _entries)
            {
                entry.Channel = ChannelFor(entry);
                CountChannel(entry.Channel, 1);
            }

            ChannelsReassigned?.Invoke();
        }

        /// <summary>Drops every retained entry. Sequence numbers keep counting so ids stay unique.</summary>
        public void Clear()
        {
            _entries.Clear();
            Array.Clear(_severityCounts, 0, _severityCounts.Length);
            _channelCounts.Clear();
            Cleared?.Invoke();
        }

        private void Insert(LogEntry entry)
        {
            entry.Channel = ChannelFor(entry);

            if (_entries.Append(entry, out LogEntry evicted))
            {
                if (evicted.Kind == LogEntryKind.Log)
                {
                    _severityCounts[(int)evicted.Severity]--;
                }

                CountChannel(evicted.Channel, -1);
                EntryEvicted?.Invoke(evicted);
            }

            if (entry.Kind == LogEntryKind.Log)
            {
                _severityCounts[(int)entry.Severity]++;
            }

            CountChannel(entry.Channel, 1);
        }

        private string ChannelFor(LogEntry entry)
        {
            return entry.Kind == LogEntryKind.Log && ChannelExtractor != null
                ? ChannelExtractor.Extract(entry.Message)
                : string.Empty;
        }

        private void CountChannel(string channel, int delta)
        {
            if (string.IsNullOrEmpty(channel))
            {
                return;
            }

            _channelCounts.TryGetValue(channel, out int count);
            count += delta;
            if (count > 0)
            {
                _channelCounts[channel] = count;
            }
            else
            {
                _channelCounts.Remove(channel);
            }
        }
    }
}
