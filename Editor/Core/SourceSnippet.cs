using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>A window of source lines around the line a stack frame points at.</summary>
    internal sealed class SourceSnippet
    {
        public SourceSnippet(string filePath, int firstLine, IReadOnlyList<string> lines, int highlightIndex)
        {
            FilePath = filePath;
            FirstLine = firstLine;
            Lines = lines;
            HighlightIndex = highlightIndex;
        }

        /// <summary>Absolute path the lines were read from.</summary>
        public string FilePath { get; }

        /// <summary>One-based line number of <c>Lines[0]</c>.</summary>
        public int FirstLine { get; }

        public IReadOnlyList<string> Lines { get; }

        /// <summary>Index into <see cref="Lines"/> of the frame's own line, or -1 when it is out of range.</summary>
        public int HighlightIndex { get; }

        /// <summary>One-based line number of the frame's own line.</summary>
        public int HighlightLine => HighlightIndex < 0 ? 0 : FirstLine + HighlightIndex;
    }
}
