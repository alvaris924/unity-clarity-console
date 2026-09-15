using ClarityConsole.Core;
using UnityEditor;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Editor entry point. Creates the single capture for this domain, starts it as early as the Editor
    /// allows, and stops it before the next assembly reload so the previous log handler is restored.
    /// </summary>
    [InitializeOnLoad]
    internal static class LogCaptureBootstrap
    {
        private const string SessionKey = "ClarityConsole.PlaySession";

        static LogCaptureBootstrap()
        {
            Store = new LogStore();
            Capture = new LogCapture(Store, sessionStateKey: SessionKey);
            Capture.Start();
            AssemblyReloadEvents.beforeAssemblyReload += Capture.Stop;
        }

        public static LogStore Store { get; }

        public static LogCapture Capture { get; }
    }
}
