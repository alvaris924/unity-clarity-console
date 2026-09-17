using System;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Decides which stack frames are infrastructure rather than the user's own code. The engine, the
    /// Editor, the runtime and this package's capture path are known noise; projects add their own
    /// wrappers, logging helpers and async plumbing as type-name prefixes.
    /// </summary>
    internal sealed class FrameFilter
    {
        public static readonly FrameFilter ShowEverything = new FrameFilter(false, false, null);

        private readonly string[] _prefixes;

        public FrameFilter(bool hideEngineFrames, IEnumerable<string> hiddenTypePrefixes)
            : this(hideEngineFrames, false, hiddenTypePrefixes)
        {
        }

        public FrameFilter(bool hideEngineFrames, bool hidePackageFrames, IEnumerable<string> hiddenTypePrefixes)
        {
            HideEngineFrames = hideEngineFrames;
            HidePackageFrames = hidePackageFrames;

            var prefixes = new List<string>();
            if (hiddenTypePrefixes != null)
            {
                foreach (string prefix in hiddenTypePrefixes)
                {
                    string trimmed = prefix?.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        prefixes.Add(trimmed);
                    }
                }
            }

            _prefixes = prefixes.ToArray();
        }

        public bool HideEngineFrames { get; }

        /// <summary>Fold frames compiled from installed packages under <c>Library/PackageCache</c>.</summary>
        public bool HidePackageFrames { get; }

        /// <summary>Type-name prefixes treated as noise, in the order configured.</summary>
        public IReadOnlyList<string> HiddenTypePrefixes => _prefixes;

        /// <summary>True when the filter would hide nothing at all.</summary>
        public bool IsEmpty => !HideEngineFrames && !HidePackageFrames && _prefixes.Length == 0;

        public bool IsNoise(TraceFrame frame)
        {
            if (frame == null)
            {
                return false;
            }

            if (HideEngineFrames && frame.IsEngineFrame)
            {
                return true;
            }

            if (HidePackageFrames && frame.IsPackageFrame)
            {
                return true;
            }

            for (int i = 0; i < _prefixes.Length; i++)
            {
                if (frame.TypeName.StartsWith(_prefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses one prefix per line, ignoring blanks and lines starting with #.</summary>
        public static List<string> ParsePrefixes(string text)
        {
            var prefixes = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return prefixes;
            }

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0 && trimmed[0] != '#')
                {
                    prefixes.Add(trimmed);
                }
            }

            return prefixes;
        }
    }
}
