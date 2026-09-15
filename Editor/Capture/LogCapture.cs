using System;
using System.Collections.Concurrent;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading;
using ClarityConsole.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Captures every log the Editor sees into a <see cref="LogStore"/>. Any thread may deliver a message;
    /// only the main thread touches the store, during <see cref="Drain"/>. The hot path allocates the entry
    /// and nothing else: no parsing, no engine calls.
    /// </summary>
    internal sealed class LogCapture : IDisposable
    {
        public const int DefaultMaxQueued = 100_000;
        public static readonly TimeSpan DefaultDrainBudget = TimeSpan.FromMilliseconds(2);

        private readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly int _maxQueued;
        private readonly string _sessionStateKey;
        private LogHandlerWrapper _wrapper;
        private ILogHandler _previousHandler;
        private int _mainThreadId;
        private int _queued;
        private int _dropped;
        private volatile int _frame;
        private int _tick;

        /// <param name="store">Destination for entries. Owned by the caller.</param>
        /// <param name="maxQueued">Hard cap on undrained entries; beyond it messages are counted as dropped.</param>
        /// <param name="sessionStateKey">
        /// When set, the Play session counter is persisted in <see cref="SessionState"/> under this key so it
        /// survives the domain reload that entering Play mode causes. Null keeps it in memory only.
        /// </param>
        public LogCapture(LogStore store, int maxQueued = DefaultMaxQueued, string sessionStateKey = null)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            _maxQueued = maxQueued;
            _sessionStateKey = sessionStateKey;
        }

        public LogStore Store { get; }

        public bool IsStarted { get; private set; }

        /// <summary>Entries delivered but not yet drained into the store.</summary>
        public int Pending => _queued;

        /// <summary>
        /// True when another package replaced the log handler after us, so ours could not be uninstalled.
        /// The chain keeps working; the wrapper merely records a context nobody reads any more.
        /// </summary>
        public bool HandlerChainBroken { get; private set; }

        /// <summary>Raised on the main thread after a drain that moved at least one entry into the store.</summary>
        public event Action Drained;

        /// <summary>Must be called on the main thread.</summary>
        public void Start()
        {
            Start("Domain loaded");
        }

        /// <summary>Must be called on the main thread. <paramref name="marker"/> is appended first; null appends nothing.</summary>
        public void Start(string marker)
        {
            if (IsStarted)
            {
                return;
            }

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (_sessionStateKey != null)
            {
                Store.CurrentSession = Math.Max(Store.CurrentSession, SessionState.GetInt(_sessionStateKey, 0));
            }

            _previousHandler = Debug.unityLogger.logHandler;
            _wrapper = new LogHandlerWrapper(_previousHandler);
            Debug.unityLogger.logHandler = _wrapper;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            IsStarted = true;

            if (marker != null)
            {
                AppendMarker(marker);
            }
        }

        /// <summary>Unsubscribes and restores the previous log handler. Safe to call more than once.</summary>
        public void Stop()
        {
            if (!IsStarted)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;

            if (ReferenceEquals(Debug.unityLogger.logHandler, _wrapper))
            {
                Debug.unityLogger.logHandler = _previousHandler;
            }
            else
            {
                HandlerChainBroken = true;
            }

            IsStarted = false;
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Moves queued entries into the store until the queue is empty or the budget is spent.
        /// Main thread only. Returns the number of entries moved.
        /// </summary>
        public int Drain(TimeSpan budget)
        {
            _stopwatch.Restart();
            int moved = 0;

            while (_queue.TryDequeue(out LogEntry entry))
            {
                Interlocked.Decrement(ref _queued);
                Store.Append(entry);
                moved++;

                if (_stopwatch.Elapsed > budget)
                {
                    break;
                }
            }

            int dropped = Interlocked.Exchange(ref _dropped, 0);
            if (dropped > 0)
            {
                Store.Append(LogEntry.Marker($"{dropped} entries dropped: capture queue exceeded {_maxQueued}", DateTime.UtcNow, _frame));
                moved++;
            }

            if (moved > 0)
            {
                Drained?.Invoke();
            }

            return moved;
        }

        public int DrainAll()
        {
            return Drain(TimeSpan.MaxValue);
        }

        private void OnEditorUpdate()
        {
            _tick++;
            _frame = EditorApplication.isPlaying ? Time.frameCount : _tick;
            Drain(DefaultDrainBudget);
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (_queued >= _maxQueued)
            {
                Interlocked.Increment(ref _dropped);
                return;
            }

            int threadId = Thread.CurrentThread.ManagedThreadId;
            var entry = new LogEntry(
                LogEntryKind.Log,
                SeverityMapping.FromLogType(type),
                condition,
                stackTrace,
                DateTime.UtcNow,
                _frame,
                threadId,
                threadId == _mainThreadId,
                PendingContext.Peek());

            Interlocked.Increment(ref _queued);
            _queue.Enqueue(entry);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    Store.CurrentSession++;
                    if (_sessionStateKey != null)
                    {
                        SessionState.SetInt(_sessionStateKey, Store.CurrentSession);
                    }

                    AppendMarker($"Entered Play mode, session {Store.CurrentSession}");
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    AppendMarker("Exiting Play mode");
                    break;
            }
        }

        private void AppendMarker(string message)
        {
            // Everything logged before the marker must land before it.
            DrainAll();
            Store.Append(LogEntry.Marker(message, DateTime.UtcNow, _frame));
        }
    }
}
