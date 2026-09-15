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
        private readonly bool[] _severityVisible = new bool[SeverityCount];
        private string _search = string.Empty;
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
            Rebuild();
        }

        public event Action<ViewChange> Changed;

        public LogStore Store { get; }

        /// <summary>Rows in display order. Owned by the view model; readers must not mutate it.</summary>
        public List<LogEntry> Visible => _visible;

        /// <summary>True when evicted entries are still listed and the next <see cref="Flush"/> will rebuild.</summary>
        public bool NeedsRebuild => _pendingEvictions > 0;

        /// <summary>Case-insensitive substring applied to the message. Markers always pass.</summary>
        public string Search
        {
            get => _search;
            set
            {
                value = value ?? string.Empty;
                if (_search == value)
                {
                    return;
                }

                _search = value;
                Rebuild();
            }
        }

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
            _pendingEvictions = 0;

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

        private bool Add(LogEntry entry)
        {
            if (!Matches(entry))
            {
                return false;
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

            return _search.Length == 0 || entry.Message.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
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
