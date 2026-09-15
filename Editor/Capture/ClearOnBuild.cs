using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Empties the console when a player build starts, if the user asked for that. Runs first so the
    /// build's own output is all that remains.
    /// </summary>
    internal sealed class ClearOnBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            LogCaptureBootstrap.ClearPolicy.Handle(ClearTrigger.BuildStarted);
        }
    }
}
