using System;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Which frame of an entry's stack counts as "the code that logged it": the same choice the detail
    /// pane opens on double-click, which prefers the project's own code, falling back to the first frame
    /// that is not engine or package code even when it has no source location.
    /// </summary>
    internal static class CallerFrame
    {
        public static TraceFrame Of(LogEntry entry)
        {
            if (entry == null || entry.Kind != LogEntryKind.Log || string.IsNullOrEmpty(entry.StackTrace))
            {
                return null;
            }

            ParsedTrace trace = entry.Trace;
            if (trace.EntryFrame != null)
            {
                return trace.EntryFrame;
            }

            foreach (TraceFrame frame in trace.Frames)
            {
                if (frame.TypeName.Length > 0 && !frame.IsEngineFrame && !frame.IsPackageFrame)
                {
                    return frame;
                }
            }

            return null;
        }

        /// <summary>The tag a caller-based rule would use: the type's short name, or empty without a caller.</summary>
        public static string TagFor(LogEntry entry)
        {
            TraceFrame frame = Of(entry);
            if (frame == null)
            {
                return string.Empty;
            }

            string name = ShortTypeName(frame.TypeName);
            return name.Length > 0 ? name : FileStem(frame.FilePath);
        }

        /// <summary>
        /// "Game.Boss.EnemySpawner/<>c__DisplayClass3_0" becomes "EnemySpawner", as does
        /// "Game.Boss.EnemySpawner+Nested"; a generic "Singleton`1<Foo>" becomes "Singleton", and a stray
        /// trailing colon from an unusual trace line is dropped.
        /// </summary>
        public static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            string outer = typeName.TrimEnd(':', ' ');
            int cut = outer.IndexOfAny(new[] { '/', '+', '`', '<', '[' });
            if (cut > 0)
            {
                outer = outer.Substring(0, cut);
            }

            int dot = outer.LastIndexOf('.');
            return dot >= 0 ? outer.Substring(dot + 1) : outer;
        }

        /// <summary>"Assets/Game/EnemySpawner.cs" becomes "EnemySpawner.cs".</summary>
        public static string FileName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            int slash = filePath.LastIndexOfAny(new[] { '/', '\\' });
            return slash >= 0 ? filePath.Substring(slash + 1) : filePath;
        }

        private static string FileStem(string filePath)
        {
            string name = FileName(filePath);
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }
    }
}
