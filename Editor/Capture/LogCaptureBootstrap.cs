using System;
using System.Collections.Generic;
using System.IO;
using ClarityConsole.Core;
using ClarityConsole.Settings;
using UnityEditor;
using UnityEngine;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Editor entry point. Restores the journal into a fresh store, starts the single capture for this
    /// domain, journals every new entry, and shuts both down before an assembly reload or Editor quit so
    /// the previous log handler is restored and the journal is flushed.
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

            Store = new LogStore { ChannelExtractor = new ChannelExtractor(ClarityConsoleSettings.instance.ChannelPattern) };
            Journal = new LogJournal(JournalDirectory);
            RestoreJournal(freshEditorSession);

            Store.EntryAppended += Journal.Append;
            Store.Cleared += Journal.Reset;

            Capture = new LogCapture(Store, sessionStateKey: PlaySessionKey);
            Capture.Drained += Journal.Flush;
            Capture.Start(freshEditorSession ? "Editor started" : "Domain reloaded");

            ClarityConsoleSettings.Changed += OnSettingsChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static LogStore Store { get; }

        public static LogJournal Journal { get; }

        public static LogCapture Capture { get; }

        /// <summary>Number of entries read back from the journal when this domain loaded.</summary>
        public static int RestoredCount { get; private set; }

        private static string JournalDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "ClarityConsole"); }
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

        private static void OnSettingsChanged()
        {
            Store.ReassignChannels(new ChannelExtractor(ClarityConsoleSettings.instance.ChannelPattern));
        }

        private static void Shutdown()
        {
            ClarityConsoleSettings.Changed -= OnSettingsChanged;
            Capture.Stop();
            Journal.Flush();
            Journal.Dispose();
        }
    }
}
