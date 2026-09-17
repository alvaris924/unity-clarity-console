using System;
using System.Text;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Removes the frames that exist only because this package wraps Unity's log handler. Unity records
    /// the stack from inside its handler, so with the wrapper installed every trace gains a
    /// <c>UnityEngine.DebugLogHandler</c> line, a <c>ClarityConsole.Capture</c> line and, for exceptions,
    /// a <c>Debug:CallOverridenDebugHandler</c> line that the stock console never shows. Dropping them
    /// gives the trace the shape it would have had without the console installed.
    /// </summary>
    internal static class CaptureFrameStripper
    {
        private static readonly string[] Prefixes =
        {
            "UnityEngine.DebugLogHandler:",
            "ClarityConsole.Capture.",
            "UnityEngine.Debug:CallOverridenDebugHandler",
        };

        /// <summary>The trace without the capture frames; the same instance when there is nothing to drop.</summary>
        public static string Strip(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace) || !ContainsCaptureFrame(stackTrace))
            {
                return stackTrace;
            }

            var kept = new StringBuilder(stackTrace.Length);
            int start = 0;
            while (start < stackTrace.Length)
            {
                int newline = stackTrace.IndexOf('\n', start);
                int end = newline < 0 ? stackTrace.Length : newline + 1;
                if (!IsCaptureFrame(stackTrace, start, end))
                {
                    kept.Append(stackTrace, start, end - start);
                }

                start = end;
            }

            return kept.ToString();
        }

        private static bool ContainsCaptureFrame(string stackTrace)
        {
            for (int i = 0; i < Prefixes.Length; i++)
            {
                if (stackTrace.IndexOf(Prefixes[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCaptureFrame(string stackTrace, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(stackTrace[start]))
            {
                start++;
            }

            for (int i = 0; i < Prefixes.Length; i++)
            {
                string prefix = Prefixes[i];
                if (end - start >= prefix.Length && string.CompareOrdinal(stackTrace, start, prefix, 0, prefix.Length) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
