using System;
using System.Collections.Generic;
using System.IO;
using ClarityConsole.Core;
using ClarityConsole.Settings;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Editor entry point. Restores the journal into a fresh store, starts the single capture for this
    /// domain, journals every new entry, applies the clear and pause preferences, and shuts everything
    /// down before an assembly reload or Editor quit so the previous log handler is restored and the
    /// journal is flushed.
    /// </summary>
    [InitializeOnLoad]
    internal static class LogCaptureBootstrap
    {
        private const string PlaySessionKey = "ClarityConsole.PlaySession";
        private const string EditorSessionKey = "ClarityConsole.EditorSessionSeen";

        static LogCaptureBootstrap()
        {
            // SessionState survives domain reloads but not an Editor restart, so its absence means a
            // fresh Editor session: restored context ids are meaningless and the marker says so.
            bool freshEditorSession = !SessionState.GetBool(EditorSessionKey, false);
            SessionState.SetBool(EditorSessionKey, true);

            Store = new LogStore
            {
                ChannelExtractor = new ChannelExtractor(ClarityConsoleSettings.instance.ChannelPattern),
                TagRules = ClarityConsoleSettings.instance.CreateTagRules(),
                AutoTagByCaller = ClarityConsoleSettings.instance.AutoTagByCaller,
                WatchExtractor = ClarityConsoleSettings.instance.CreateWatchExtractor(),
            };
            Journal = new LogJournal(JournalDirectory);
            RestoreJournal(freshEditorSession);

            Store.EntryAppended += Journal.Append;
            Store.Cleared += Journal.Reset;

            ClearPolicy = new ClearPolicy(Store, ShouldClear, () => ConsolePreferences.ErrorPause);
            ClearPolicy.PauseRequested += PausePlayMode;
            Store.EntryAppended += OnEntryAppended;

            Capture = new LogCapture(Store, sessionStateKey: PlaySessionKey);
            Capture.Drained += Journal.Flush;
            Capture.Start(freshEditorSession ? LogEntry.EditorStartedMarker : LogEntry.DomainReloadMarker);

            ClarityConsoleSettings.Changed += OnSettingsChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static LogStore Store { get; }

        public static LogJournal Journal { get; }

        public static LogCapture Capture { get; }

        /// <summary>Decides when the console empties itself and when an error pauses Play mode.</summary>
        public static ClearPolicy ClearPolicy { get; }

        /// <summary>Number of entries read back from the journal when this domain loaded.</summary>
        public static int RestoredCount { get; private set; }

        private static string JournalDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "ClarityConsole"); }
        }

        private static bool ShouldClear(ClearTrigger trigger)
        {
            switch (trigger)
            {
                case ClearTrigger.EnteringPlayMode:
                    return ConsolePreferences.ClearOnPlay;
                case ClearTrigger.CompilationStarted:
                    return ConsolePreferences.ClearOnRecompile;
                case ClearTrigger.BuildStarted:
                    return ConsolePreferences.ClearOnBuild;
                default:
                    return false;
            }
        }

        private static void RestoreJournal(bool freshEditorSession)
        {
            List<LogEntry> restored;
            try
            {
                restored = Journal.Load(dropContexts: freshEditorSession);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogWarning("[ClarityConsole] The log journal could not be read and was reset: " + ex.Message);
                Journal.Reset();
                return;
            }

            foreach (LogEntry entry in restored)
            {
                Store.Restore(entry);
            }

            RestoredCount = restored.Count;
        }

        private static void OnEntryAppended(LogEntry entry)
        {
            ClearPolicy.Inspect(entry, EditorApplication.isPlaying);
        }

        private static void PausePlayMode()
        {
            EditorApplication.isPaused = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Clearing while leaving Edit mode keeps the "Entered Play mode" marker, which is appended
            // afterwards, as the first thing in the run.
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                ClearPolicy.Handle(ClearTrigger.EnteringPlayMode);
            }
        }

        private static void OnCompilationStarted(object context)
        {
            // Clearing now also resets the journal, so nothing is restored after the reload.
            ClearPolicy.Handle(ClearTrigger.CompilationStarted);
        }

        private static void OnSettingsChanged()
        {
            Store.WatchExtractor = ClarityConsoleSettings.instance.CreateWatchExtractor();
            Store.ReassignChannels(
                new ChannelExtractor(ClarityConsoleSettings.instance.ChannelPattern),
                ClarityConsoleSettings.instance.CreateTagRules(),
                ClarityConsoleSettings.instance.AutoTagByCaller);
        }

        private static void Shutdown()
        {
            ClarityConsoleSettings.Changed -= OnSettingsChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            Store.EntryAppended -= OnEntryAppended;
            Capture.Stop();
            Journal.Flush();
            Journal.Dispose();
        }
    }
}
