using System.Collections.Generic;
using ClarityConsole.Core;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace ClarityConsole.UI
{
    /// <summary>
    /// Builds the header that goes on top of an export or a bug report: which Editor produced it, which
    /// package version, and what the console was showing at the time. Without that, a pasted log is hard
    /// to act on.
    /// </summary>
    internal static class ExportContext
    {
        public static List<string> Describe(ConsoleViewModel viewModel)
        {
            var lines = new List<string>
            {
                "Exported " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Unity " + Application.unityVersion + " on " + Application.platform,
                "Clarity Console " + PackageVersion(),
            };

            if (viewModel == null)
            {
                return lines;
            }

            LogStore store = viewModel.Store;
            lines.Add("Session " + store.CurrentSession + ", " + store.Count + " entries captured, " + viewModel.Visible.Count + " shown");

            var filters = new List<string>();
            if (viewModel.Search.Length > 0)
            {
                filters.Add("query \"" + viewModel.Search + "\"");
            }

            if (viewModel.SelectedChannels.Count > 0)
            {
                filters.Add("channels " + string.Join(", ", viewModel.SelectedChannels));
            }

            var hiddenSeverities = new List<string>();
            foreach (LogSeverity severity in System.Enum.GetValues(typeof(LogSeverity)))
            {
                if (!viewModel.IsSeverityVisible(severity))
                {
                    hiddenSeverities.Add(severity.ToString());
                }
            }

            if (hiddenSeverities.Count > 0)
            {
                filters.Add("hiding " + string.Join(", ", hiddenSeverities));
            }

            if (viewModel.IgnoredCount > 0)
            {
                filters.Add(viewModel.IgnoredCount + " ignored by rules");
            }

            lines.Add(filters.Count == 0 ? "No filters active" : "Filters: " + string.Join("; ", filters));
            return lines;
        }

        private static string PackageVersion()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(ExportContext).Assembly);
            return package == null ? "(unknown version)" : package.version;
        }
    }
}
