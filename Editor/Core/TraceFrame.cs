namespace ClarityConsole.Core
{
    /// <summary>One line of a parsed stack trace.</summary>
    internal sealed class TraceFrame
    {
        public TraceFrame(string raw, string signature, string typeName, string methodName, string filePath, int line)
        {
            Raw = raw;
            Signature = signature;
            TypeName = typeName;
            MethodName = methodName;
            FilePath = filePath ?? string.Empty;
            Line = line;
        }

        /// <summary>The line exactly as Unity delivered it.</summary>
        public string Raw { get; }

        /// <summary>Method text without the location suffix, for example <c>Foo.Bar (System.Int32)</c>.</summary>
        public string Signature { get; }

        /// <summary>Declaring type including its namespace; empty when the line had no separator.</summary>
        public string TypeName { get; }

        public string MethodName { get; }

        /// <summary>Forward-slash path as printed, project-relative or absolute; empty without a location.</summary>
        public string FilePath { get; }

        /// <summary>One-based line number; 0 without a location.</summary>
        public int Line { get; }

        public bool HasLocation => FilePath.Length > 0 && Line > 0;

        /// <summary>
        /// True for frames the user did not write: the engine, the Editor, the runtime, the test framework,
        /// and this package's own capture path, which sits above every user frame in the trace.
        /// </summary>
        public bool IsEngineFrame
        {
            get
            {
                return TypeName.StartsWith("UnityEngine.", System.StringComparison.Ordinal)
                    || TypeName.StartsWith("ClarityConsole.Capture.", System.StringComparison.Ordinal)
                    || TypeName.StartsWith("UnityEditor.", System.StringComparison.Ordinal)
                    || TypeName.StartsWith("System.", System.StringComparison.Ordinal)
                    || TypeName.StartsWith("Mono.", System.StringComparison.Ordinal)
                    || TypeName.StartsWith("NUnit.", System.StringComparison.Ordinal);
            }
        }

        public override string ToString() => Raw;
    }
}
