using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>A stack trace split into frames, with the frame a user most likely wants to open.</summary>
    internal sealed class ParsedTrace
    {
        public static readonly ParsedTrace Empty = new ParsedTrace(new List<TraceFrame>());

        public ParsedTrace(IReadOnlyList<TraceFrame> frames)
        {
            Frames = frames;
            EntryFrame = FindEntryFrame(frames);
        }

        public IReadOnlyList<TraceFrame> Frames { get; }

        /// <summary>
        /// The frame to open on double-click: the first located frame under <c>Assets/</c>, otherwise the first
        /// located frame outside the engine and the package cache, otherwise any located frame, otherwise null.
        /// </summary>
        public TraceFrame EntryFrame { get; }

        private static TraceFrame FindEntryFrame(IReadOnlyList<TraceFrame> frames)
        {
            TraceFrame outsideEngine = null;
            TraceFrame anyLocated = null;

            for (int i = 0; i < frames.Count; i++)
            {
                TraceFrame frame = frames[i];
                if (!frame.HasLocation)
                {
                    continue;
                }

                anyLocated = anyLocated ?? frame;
                if (frame.IsEngineFrame)
                {
                    continue;
                }

                if (frame.FilePath.StartsWith("Assets/", System.StringComparison.Ordinal))
                {
                    return frame;
                }

                if (outsideEngine == null && !frame.FilePath.StartsWith("Library/", System.StringComparison.Ordinal))
                {
                    outsideEngine = frame;
                }
            }

            return outsideEngine ?? anyLocated;
        }
    }
}
