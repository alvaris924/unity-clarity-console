using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>What a step in a <see cref="CallFlow"/> stands for.</summary>
    internal enum FlowStepKind
    {
        /// <summary>One call: a single stack frame.</summary>
        Call,

        /// <summary>A run of infrastructure frames shown as one step until it is unfolded.</summary>
        Folded,
    }

    /// <summary>One step of the path that led to an entry, in the order the calls were made.</summary>
    internal sealed class FlowStep
    {
        public FlowStep(FlowStepKind kind, IReadOnlyList<TraceFrame> frames, int groupIndex, bool isEntry, bool isOrigin)
        {
            Kind = kind;
            Frames = frames;
            GroupIndex = groupIndex;
            IsEntry = isEntry;
            IsOrigin = isOrigin;
            Name = kind == FlowStepKind.Call ? CallName.For(frames[0]) : CallName.Empty;
        }

        public FlowStepKind Kind { get; }

        /// <summary>The frames behind this step, outermost call first. A call step has exactly one.</summary>
        public IReadOnlyList<TraceFrame> Frames { get; }

        /// <summary>The frame of a call step; the outermost frame of a folded one.</summary>
        public TraceFrame Frame => Frames[0];

        /// <summary>Index of the <see cref="FrameGroup"/> the step came from, which is what unfolding names.</summary>
        public int GroupIndex { get; }

        /// <summary>The frame a reader most likely wants: the same one the detail pane emphasises and opens.</summary>
        public bool IsEntry { get; }

        /// <summary>The innermost step: where the message was logged or the exception thrown.</summary>
        public bool IsOrigin { get; }

        /// <summary>A readable name for a call step; empty for a folded one.</summary>
        public CallName Name { get; }
    }

    /// <summary>
    /// The short, readable name of a call: <c>EnemySpawner.Spawn</c> rather than
    /// <c>Game.Enemies.EnemySpawner/&lt;Spawn&gt;d__4:MoveNext</c>, with a note when the compiler
    /// generated the method for a coroutine, an async method, a lambda or a local function.
    /// </summary>
    internal readonly struct CallName
    {
        public static readonly CallName Empty = new CallName(string.Empty, string.Empty, CallNote.None);

        private static readonly Regex StateMachine = new Regex(@"^<(?<name>[^>]+)>d__\d+$", RegexOptions.Compiled);
        private static readonly Regex Lambda = new Regex(@"^<(?<name>[^>]*)>b__[\w]+$", RegexOptions.Compiled);
        private static readonly Regex LocalFunction = new Regex(@"^<(?<outer>[^>]+)>g__(?<name>[^|]+)\|[\w]+$", RegexOptions.Compiled);
        private static readonly Regex DisplayClass = new Regex(@"^<>c(__DisplayClass[\w]+)?$", RegexOptions.Compiled);

        public CallName(string owner, string method, CallNote note)
        {
            Owner = owner;
            Method = method;
            Note = note;
        }

        /// <summary>The declaring type without its namespace; the outer type for compiler-generated ones.</summary>
        public string Owner { get; }

        public string Method { get; }

        public CallNote Note { get; }

        public override string ToString() => Owner.Length == 0 ? Method : Owner + "." + Method;

        public static CallName For(TraceFrame frame)
        {
            if (frame == null)
            {
                return Empty;
            }

            if (frame.TypeName.Length == 0 && frame.MethodName.Length == 0)
            {
                return new CallName(string.Empty, frame.Signature, CallNote.None);
            }

            // Nested types print with '/' in Editor traces and '+' in exception traces.
            string[] parts = StripNamespace(frame.TypeName).Split('/', '+');
            string last = parts[parts.Length - 1];
            string outer = parts.Length > 1 ? parts[parts.Length - 2] : last;
            string method = frame.MethodName;

            Match machine = StateMachine.Match(last);
            if (machine.Success && parts.Length > 1)
            {
                // A coroutine or an async method: MoveNext is the compiler's name for "resumed here".
                return new CallName(outer, machine.Groups["name"].Value, CallNote.Resumed);
            }

            Match local = LocalFunction.Match(method);
            if (local.Success)
            {
                string owner = DisplayClass.IsMatch(last) && parts.Length > 1 ? outer : last;
                return new CallName(owner, local.Groups["outer"].Value + "." + local.Groups["name"].Value, CallNote.LocalFunction);
            }

            Match lambda = Lambda.Match(method);
            if (lambda.Success)
            {
                string owner = DisplayClass.IsMatch(last) && parts.Length > 1 ? outer : last;
                string inside = lambda.Groups["name"].Value;
                return new CallName(owner, inside.Length > 0 ? inside : "lambda", CallNote.Lambda);
            }

            return new CallName(last, method, CallNote.None);
        }

        /// <summary><c>Game.Enemies.Spawner</c> becomes <c>Spawner</c>; dots inside generic arguments are left alone.</summary>
        internal static string StripNamespace(string typeName)
        {
            int depth = 0;
            for (int i = typeName.Length - 1; i >= 0; i--)
            {
                char c = typeName[i];
                if (c == '>' || c == ']')
                {
                    depth++;
                }
                else if (c == '<' || c == '[')
                {
                    depth--;
                }
                else if (depth == 0 && c == '.')
                {
                    // Nested-type separators ('/' and '+') are not dots, so the scan passes them and stops
                    // where the namespace ends, before the outermost type.
                    return typeName.Substring(i + 1);
                }
            }

            return typeName;
        }
    }

    /// <summary>Why a call's name reads differently from its frame.</summary>
    internal enum CallNote
    {
        None,

        /// <summary>A coroutine or async method picked up again after a yield or an await.</summary>
        Resumed,

        Lambda,

        LocalFunction,
    }

    /// <summary>
    /// Turns an entry's stack trace into the path that led to it, read top to bottom: the outermost call
    /// first, the line that logged or threw last. Unity prints traces the other way round, innermost
    /// first. Infrastructure runs fold into single steps by the same rules as the detail pane, including
    /// the one that leaves an error made only of infrastructure frames unfolded.
    /// </summary>
    internal static class CallFlow
    {
        /// <param name="expanded">Group indices the reader has unfolded; null unfolds nothing.</param>
        public static List<FlowStep> Build(LogEntry entry, FrameFilter filter, ICollection<int> expanded = null)
        {
            var steps = new List<FlowStep>();
            if (entry == null || entry.Kind != LogEntryKind.Log)
            {
                return steps;
            }

            ParsedTrace trace = entry.Trace;
            List<FrameGroup> groups = FrameGrouper.Group(trace, filter);
            if (groups.Count == 0)
            {
                return steps;
            }

            TraceFrame entryFrame = FrameGrouper.FindEntryFrame(trace, filter);
            bool unfoldAll = IsFailure(entry.Severity) && groups.Count == 1 && groups[0].IsNoise;
            TraceFrame innermost = trace.Frames[0];

            for (int g = groups.Count - 1; g >= 0; g--)
            {
                FrameGroup group = groups[g];
                if (group.IsNoise && !unfoldAll && (expanded == null || !expanded.Contains(g)))
                {
                    var outermostFirst = new List<TraceFrame>(group.Frames);
                    outermostFirst.Reverse();
                    steps.Add(new FlowStep(FlowStepKind.Folded, outermostFirst, g, false, ContainsFrame(group, innermost)));
                    continue;
                }

                for (int f = group.Frames.Count - 1; f >= 0; f--)
                {
                    TraceFrame frame = group.Frames[f];
                    steps.Add(new FlowStep(
                        FlowStepKind.Call,
                        new[] { frame },
                        g,
                        ReferenceEquals(frame, entryFrame),
                        ReferenceEquals(frame, innermost)));
                }
            }

            return steps;
        }

        /// <summary>True when the flow would have anything to show for this entry.</summary>
        public static bool CanShow(LogEntry entry)
        {
            return entry != null && entry.Kind == LogEntryKind.Log && entry.Trace.Frames.Count > 0;
        }

        private static bool ContainsFrame(FrameGroup group, TraceFrame frame)
        {
            foreach (TraceFrame candidate in group.Frames)
            {
                if (ReferenceEquals(candidate, frame))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFailure(LogSeverity severity)
        {
            return severity == LogSeverity.Error || severity == LogSeverity.Exception || severity == LogSeverity.Assert;
        }
    }
}
