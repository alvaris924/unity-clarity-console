using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>A run of stack frames that are shown together, either as themselves or as one folded row.</summary>
    internal sealed class FrameGroup
    {
        public FrameGroup(bool isNoise, IReadOnlyList<TraceFrame> frames)
        {
            IsNoise = isNoise;
            Frames = frames;
        }

        /// <summary>True when these frames are infrastructure and are folded into one row.</summary>
        public bool IsNoise { get; }

        public IReadOnlyList<TraceFrame> Frames { get; }

        public int Count => Frames.Count;
    }

    /// <summary>
    /// Folds consecutive infrastructure frames into single groups so the user's own code stands out.
    /// The entry frame always stays visible, and a trace with nothing but noise is left alone: hiding
    /// every frame would leave the reader with nothing.
    /// </summary>
    internal static class FrameGrouper
    {
        /// <summary>
        /// The frame to open for a trace once noise rules are applied: the same choice
        /// <see cref="ParsedTrace.EntryFrame"/> makes, but over the frames the filter keeps, so a
        /// project's own logging wrapper is skipped too. Falls back to the unfiltered choice when every
        /// located frame is noise.
        /// </summary>
        public static TraceFrame FindEntryFrame(ParsedTrace trace, FrameFilter filter)
        {
            if (trace == null)
            {
                return null;
            }

            if (filter == null || filter.IsEmpty)
            {
                return trace.EntryFrame;
            }

            var kept = new List<TraceFrame>(trace.Frames.Count);
            foreach (TraceFrame frame in trace.Frames)
            {
                if (!filter.IsNoise(frame))
                {
                    kept.Add(frame);
                }
            }

            return ParsedTrace.ChooseEntryFrame(kept) ?? trace.EntryFrame;
        }

        public static List<FrameGroup> Group(ParsedTrace trace, FrameFilter filter)
        {
            var groups = new List<FrameGroup>();
            if (trace == null || trace.Frames.Count == 0)
            {
                return groups;
            }

            if (filter == null || filter.IsEmpty)
            {
                groups.Add(new FrameGroup(false, trace.Frames));
                return groups;
            }

            TraceFrame entryFrame = FindEntryFrame(trace, filter);
            var noise = new bool[trace.Frames.Count];
            bool anyVisible = false;
            for (int i = 0; i < trace.Frames.Count; i++)
            {
                TraceFrame frame = trace.Frames[i];
                noise[i] = !ReferenceEquals(frame, entryFrame) && filter.IsNoise(frame);
                anyVisible |= !noise[i];
            }

            if (!anyVisible)
            {
                groups.Add(new FrameGroup(false, trace.Frames));
                return groups;
            }

            var run = new List<TraceFrame>();
            bool runIsNoise = noise[0];

            for (int i = 0; i < trace.Frames.Count; i++)
            {
                if (noise[i] != runIsNoise)
                {
                    groups.Add(new FrameGroup(runIsNoise, run));
                    run = new List<TraceFrame>();
                    runIsNoise = noise[i];
                }

                run.Add(trace.Frames[i]);
            }

            groups.Add(new FrameGroup(runIsNoise, run));
            return groups;
        }
    }
}
