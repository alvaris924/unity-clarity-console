namespace ClarityConsole.Core
{
    /// <summary>
    /// Severity of a captured entry, ordered from least to most severe.
    /// Deliberately independent of <c>UnityEngine.LogType</c> so the core assembly
    /// keeps no engine references; the capture layer maps between the two.
    /// </summary>
    internal enum LogSeverity
    {
        Log = 0,
        Warning = 1,
        Error = 2,
        Exception = 3,
        Assert = 4,
    }
}
