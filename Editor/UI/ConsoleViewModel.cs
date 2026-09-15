using System;
using System.Collections.Generic;
using ClarityConsole.Core;

namespace ClarityConsole.UI
{
    internal enum ViewChange
    {
        /// <summary>Rows were added or a collapse count changed; existing rows kept their index.</summary>
        Appended,

        /// <summary>The visible list was recomputed from scratch.</summary>
        Rebuilt,
    }

    /// <summary>
    /// Filters and collapses the store into the list the window shows. Main thread only. Appends are
    /// incremental, filter changes rebuild, and evictions are folded in lazily through <see cref="Flush"/>
    /// so a full buffer does not pay a rebuild per append.
    /// </summary>
    internal sealed class ConsoleViewModel : IDisposable
    {
        private static readonly int SeverityCount = Enum.GetValues(typeof(LogSeverity)).Length;

        private readonly List<LogEntry> _visible = new List<LogEntry>();
        private readonly List<int> _counts = new List<int>();
        private readonly Dictionary<CollapseKey, int> _groups = new Dictionary<CollapseKey, int>();
        private readonly Dictionary<string, int> _watchRows = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _selectedChannels = new HashSet<string>(StringComparer.Ordinal);
        private readonly bool[] _severityVisible = new bool[SeverityCount];
        private LogQuery _query = LogQuery.Empty;
        private IgnoreList _ignoreList = IgnoreList.Empty;
        private int _ignored;
        private bool _collapse;
        private int _pendingEvictions;

        public ConsoleViewModel(LogStore store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            for (int i = 0; i < _severityVisible.Length; i++)
            {
                _severityVisible[i] = true;
            }

            Store.EntryAppended += OnAppended;
            Store.EntryEvicted += OnEvicted;
            Store.Cleared += OnCleared;
            Store.ChannelsReassigned += OnChannelsReassigned;
            Rebuild();
        }

        public event Action<ViewChange> Changed;

        public LogStore Store { get; }

        /// <summary>Rows in display order. Owned by the view model; readers must not mutate it.</summary>
        public List<LogEntry> Visible => _visible;

        /// <summary>True when evicted entries are still listed and the next <see cref="Flush"/> will rebuild.</summary>
        public bool NeedsRebuild => _pendingEvictions > 0;

        /// <summary>Channels the list is narrowed to. Empty means every channel is shown.</summary>
        public IReadOnlyCollection<string> SelectedChannels => _selectedChannels;

        /// <summary>
        /// The search query, in the syntax of <see cref="LogQuery"/>. A malformed query matches nothing
        /// and explains itself through <see cref="QueryError"/>. Markers always pass.
        /// </summary>
        public string Search
        {
            get => _query.Text;
            set
            {
                value = value ?? string.Empty;
                if (_query.Text == value)
                {
                    return;
                }

                _query = LogQuery.Parse(value);
                Rebuild();
            }
        }

        /// <summary>Why the current query cannot be used, or null when it is fine.</summary>
        public string QueryError => _query.Error;

        /// <summary>Rules that silence entries. Entries stay in the store, so removing a rule brings them back.</summary>
        public IgnoreList IgnoreList
        {
            get => _ignoreList;
            set
            {
                _ignoreList = value ?? IgnoreList.Empty;
                Rebuild();
            }
        }

        /// <summary>Retained entries silenced by the ignore rules, whatever the other filters say.</summary>
        public int IgnoredCount => _ignored;

        /// <summary>Groups log rows with the same severity and message; counts are read through <see cref="CountAt"/>.</summary>
        public bool Collapse
        {
            get => _collapse;
            set
            {
                if (_collapse == value)
                {
                    return;
                }

                _collapse = value;
                Rebuild();
            }
        }

        /// <summary>Number of store entries the row at <paramref name="index"/> stands for; 1 unless collapsed.</summary>
        public int CountAt(int index) => _counts[index];

        public bool IsSeverityVisible(LogSeverity severity) => _severityVisible[(int)severity];

        public void SetSeverityVisible(LogSeverity severity, bool visible)
        {
            if (_severityVisible[(int)severity] == visible)
            {
                return;
            }

            _severityVisible[(int)severity] = visible;
            Rebuild();
        }

        public bool IsChannelSelected(string channel)
        {
            return channel != null && _selectedChannels.Contains(channel);
        }

        public void SetChannelSelected(string channel, bool selected)
        {
            if (string.IsNullOrEmpty(channel))
            {
                return;
            }

            bool changed = selected ? _selectedChannels.Add(channel) : _selectedChannels.Remove(channel);
            if (changed)
            {
                Rebuild();
            }
        }

        public void ClearChannelSelection()
        {
            if (_selectedChannels.Count == 0)
            {
                return;
            }

            _selectedChannels.Clear();
            Rebuild();
        }

        /// <summary>Folds pending evictions in. Free when nothing is pending; call once per UI refresh.</summary>
        public void Flush()
        {
            if (_pendingEvictions > 0)
            {
                Rebuild();
            }
        }

        public void Rebuild()
        {
            _visible.Clear();
            _counts.Clear();
            _groups.Clear();
            _watchRows.Clear();
            _pendingEvictions = 0;
            _ignored = 0;

            foreach (LogEntry entry in Store.Entries)
            {
                Add(entry);
            }

            Changed?.Invoke(ViewChange.Rebuilt);
        }

        public void Dispose()
        {
            Store.EntryAppended -= OnAppended;
            Store.EntryEvicted -= OnEvicted;
            Store.Cleared -= OnCleared;
            Store.ChannelsReassigned -= OnChannelsReassigned;
        }

        private void OnAppended(LogEntry entry)
        {
            if (Add(entry))
            {
                Changed?.Invoke(ViewChange.Appended);
            }
        }

        private void OnEvicted(LogEntry entry)
        {
            _pendingEvictions++;
        }

        private void OnCleared()
        {
            Rebuild();
        }

        private void OnChannelsReassigned()
        {
            // Channels that no longer exist cannot match anything, so drop them from the selection.
            _selectedChannels.RemoveWhere(channel => Store.CountOfChannel(channel) == 0);
            Rebuild();
        }

        private bool Add(LogEntry entry)
        {
            if (_ignoreList.ShouldIgnore(entry))
            {
                _ignored++;
                return false;
            }

            if (!Matches(entry))
            {
                return false;
            }

            if (entry.WatchKey.Length > 0)
            {
                // A watch row keeps its place and shows the newest value, so a value logged every frame
                // reads as one line that changes rather than a wall of near-identical rows.
                if (_watchRows.TryGetValue(entry.WatchKey, out int watchIndex))
                {
                    _visible[watchIndex] = entry;
                    _counts[watchIndex]++;
                    return true;
                }

                _watchRows[entry.WatchKey] = _visible.Count;
                _visible.Add(entry);
                _counts.Add(1);
                return true;
            }

            if (_collapse && entry.Kind == LogEntryKind.Log)
            {
                var key = new CollapseKey(entry.Severity, entry.Message);
                if (_groups.TryGetValue(key, out int index))
                {
                    _counts[index]++;
                    return true;
                }

                _groups[key] = _visible.Count;
            }

            _visible.Add(entry);
            _counts.Add(1);
            return true;
        }

        private bool Matches(LogEntry entry)
        {
            if (entry.Kind == LogEntryKind.Marker)
            {
                return true;
            }

            if (!_severityVisible[(int)entry.Severity])
            {
                return false;
            }

            if (_selectedChannels.Count > 0 && !_selectedChannels.Contains(entry.Channel))
            {
                return false;
            }

            return _query.Matches(entry);
        }

        private readonly struct CollapseKey : IEquatable<CollapseKey>
        {
            private readonly LogSeverity _severity;
            private readonly string _message;

            public CollapseKey(LogSeverity severity, string message)
            {
                _severity = severity;
                _message = message;
            }

            public bool Equals(CollapseKey other)
            {
                return _severity == other._severity && string.Equals(_message, other._message, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CollapseKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return unchecked((StringComparer.Ordinal.GetHashCode(_message) * 397) ^ (int)_severity);
            }
        }
    }
}
