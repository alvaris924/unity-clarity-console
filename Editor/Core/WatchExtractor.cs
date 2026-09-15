namespace ClarityConsole.Core
{
    /// <summary>
    /// Reads a watch key from a message prefix, by convention <c>[watch:PlayerHP] 87</c>. Entries that
    /// share a key replace each other in the window instead of piling up, which is what makes a value
    /// logged every frame readable. See <see cref="PrefixExtractor"/> for the matching and caching rules.
    /// </summary>
    internal sealed class WatchExtractor : PrefixExtractor
    {
        /// <summary>Matches a leading <c>[watch:Name]</c>, the name being letters, digits, dot, dash, underscore or space.</summary>
        public const string DefaultPattern = @"^\[watch:([\w.\- ]{1,64})\]";

        public WatchExtractor(string pattern = DefaultPattern)
            : base(pattern, DefaultPattern)
        {
        }
    }
}
