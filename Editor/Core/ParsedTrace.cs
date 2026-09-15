using System;
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
            EntryFrame = ChooseEntryFrame(frames);
        }

        public IReadOnlyList<TraceFrame> Frames { get; }

        /// <summary>
        /// The frame to open on double-click, ignoring any user-configured noise rules. Callers that have
        /// a <see cref="FrameFilter"/> should use <see cref="FrameGrouper.FindEntryFrame"/> instead, so a
        /// project's own logging wrapper is skipped as well.
        /// </summary>
        public TraceFrame EntryFrame { get; }

        /// <summary>
        /// Picks the frame a reader most likely wants: the first located frame under <c>Assets/</c>,
        /// otherwise the first located frame outside the engine and the package cache, otherwise any
        /// located frame, otherwise null.
        /// </summary>
        internal static TraceFrame ChooseEntryFrame(IReadOnlyList<TraceFrame> frames)
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

                if (frame.FilePath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    return frame;
                }

                if (outsideEngine == null && !frame.FilePath.StartsWith("Library/", StringComparison.Ordinal))
                {
                    outsideEngine = frame;
                }
            }

            return outsideEngine ?? anyLocated;
        }
    }
}
