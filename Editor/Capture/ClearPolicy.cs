using System;
using ClarityConsole.Core;

namespace ClarityConsole.Capture
{
    /// <summary>What the Editor just did, as far as clearing is concerned.</summary>
    internal enum ClearTrigger
    {
        EnteringPlayMode,
        CompilationStarted,
        BuildStarted,
    }

    /// <summary>
    /// Decides when the console empties itself and when an error should pause Play mode. The rules are
    /// kept apart from the Editor callbacks that feed them so they can be tested without an Editor.
    /// </summary>
    internal sealed class ClearPolicy
    {
        private readonly LogStore _store;
        private readonly Func<ClearTrigger, bool> _shouldClear;
        private readonly Func<bool> _errorPauseEnabled;

        /// <param name="store">The store to empty. Clearing also resets the journal through its event.</param>
        /// <param name="shouldClear">Whether the user asked to clear on that trigger.</param>
        /// <param name="errorPauseEnabled">Whether the user asked to pause on errors.</param>
        public ClearPolicy(LogStore store, Func<ClearTrigger, bool> shouldClear, Func<bool> errorPauseEnabled)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _shouldClear = shouldClear ?? throw new ArgumentNullException(nameof(shouldClear));
            _errorPauseEnabled = errorPauseEnabled ?? throw new ArgumentNullException(nameof(errorPauseEnabled));
        }

        /// <summary>Raised when Play mode should pause because an error was captured.</summary>
        public event Action PauseRequested;

        /// <summary>Clears the store when the user asked to on this trigger. Returns true when it did.</summary>
        public bool Handle(ClearTrigger trigger)
        {
            if (!_shouldClear(trigger))
            {
                return false;
            }

            _store.Clear();
            return true;
        }

        /// <summary>
        /// Called for each entry as it reaches the store. Asks for a pause on the first error while
        /// playing; markers and anything below an error are ignored, as is an error logged in Edit mode,
        /// where there is nothing to pause.
        /// </summary>
        public bool Inspect(LogEntry entry, bool isPlaying)
        {
            if (entry == null || !isPlaying || entry.Kind != LogEntryKind.Log || entry.Severity < LogSeverity.Error)
            {
                return false;
            }

            if (!_errorPauseEnabled())
            {
                return false;
            }

            PauseRequested?.Invoke();
            return true;
        }
    }
}
