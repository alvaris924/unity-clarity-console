namespace ClarityConsole.Core
{
    /// <summary>What an entry in the store represents.</summary>
    internal enum LogEntryKind
    {
        /// <summary>A message captured from the engine or from user code.</summary>
        Log = 0,

        /// <summary>A synthetic divider such as "Entered Play mode" or "Domain loaded".</summary>
        Marker = 1,
    }
}
