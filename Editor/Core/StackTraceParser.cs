using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Splits stack-trace text into <see cref="TraceFrame"/>s. Understands the Editor format
    /// (<c>Type:Method (args) (at Assets/File.cs:12)</c>), the exception format
    /// (<c>Ns.Type.Method () (at Assets/File.cs:12)</c>) and the Mono format
    /// (<c>at Ns.Type.Method () [0x0001a] in D:\File.cs:12</c>). Lines without a recognizable
    /// location become frames without one; nothing is ever dropped except blank lines.
    /// </summary>
    internal static class StackTraceParser
    {
        private static readonly Regex UnityLocation = new Regex(
            @"^(?<method>.*?)\s*\(at (?<path>.*?):(?<line>-?\d+)\)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex MonoLocation = new Regex(
            @"^\s*(?:at\s+)?(?<method>.*?)\s*(?:\[0x[0-9A-Fa-f]+\]\s*)?\bin\s+(?<path>.*?):(?<line>-?\d+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex BareFrame = new Regex(
            @"^\s*(?:at\s+)?(?<method>\S.*?)\s*$",
            RegexOptions.Compiled);

        private static readonly char[] LineSeparators = { '\n' };

        public static ParsedTrace Parse(string stackTrace)
        {
            if (string.IsNullOrWhiteSpace(stackTrace))
            {
                return ParsedTrace.Empty;
            }

            var frames = new List<TraceFrame>();
            foreach (string rawLine in stackTrace.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                frames.Add(ParseLine(line));
            }

            return new ParsedTrace(frames);
        }

        internal static TraceFrame ParseLine(string line)
        {
            Match match = UnityLocation.Match(line);
            if (!match.Success)
            {
                match = MonoLocation.Match(line);
            }

            if (match.Success)
            {
                string signature = match.Groups["method"].Value.Trim();
                string path = NormalizePath(match.Groups["path"].Value);
                int.TryParse(match.Groups["line"].Value, out int lineNumber);
                if (IsUnknownPath(path) || lineNumber <= 0)
                {
                    path = string.Empty;
                    lineNumber = 0;
                }

                SplitSignature(signature, out string typeName, out string methodName);
                return new TraceFrame(line, signature, typeName, methodName, path, lineNumber);
            }

            Match bare = BareFrame.Match(line);
            string bareSignature = bare.Success ? bare.Groups["method"].Value : line.Trim();
            SplitSignature(bareSignature, out string bareType, out string bareMethod);
            return new TraceFrame(line, bareSignature, bareType, bareMethod, string.Empty, 0);
        }

        internal static string NormalizePath(string path)
        {
            string normalized = path.Trim().Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            return normalized;
        }

        private static bool IsUnknownPath(string path)
        {
            return path.Length == 0 || path[0] == '<';
        }

        /// <summary>
        /// Splits <c>Ns.Type:Method (args)</c> or <c>Ns.Type.Method (args)</c> into type and method,
        /// ignoring separators inside generic argument lists.
        /// </summary>
        internal static void SplitSignature(string signature, out string typeName, out string methodName)
        {
            int paren = signature.IndexOf('(');
            string qualified = (paren < 0 ? signature : signature.Substring(0, paren)).Trim();

            int depth = 0;
            for (int i = qualified.Length - 1; i >= 0; i--)
            {
                char c = qualified[i];
                if (c == '>' || c == ']')
                {
                    depth++;
                }
                else if (c == '<' || c == '[')
                {
                    depth--;
                }
                else if (depth == 0 && (c == ':' || c == '.'))
                {
                    typeName = qualified.Substring(0, i);
                    methodName = qualified.Substring(i + 1);
                    return;
                }
            }

            typeName = string.Empty;
            methodName = qualified;
        }
    }
}
