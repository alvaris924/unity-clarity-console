namespace ClarityConsole.Core
{
    /// <summary>
    /// Reads the channel from a message prefix, by convention <c>[Tag] the rest of the message</c>.
    /// See <see cref="PrefixExtractor"/> for the matching and caching rules.
    /// </summary>
    internal sealed class ChannelExtractor : PrefixExtractor
    {
        /// <summary>Matches a leading <c>[Tag]</c> whose name is letters, digits, dot, dash, underscore or space.</summary>
        public const string DefaultPattern = @"^\[([\w.\- ]{1,64})\]";

        public ChannelExtractor(string pattern = DefaultPattern)
            : base(pattern, DefaultPattern)
        {
        }
    }
}
