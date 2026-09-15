using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Pulls a channel name out of a message prefix, by convention <c>[Tag] the rest of the message</c>.
    /// The pattern is user-configurable; an invalid one falls back to <see cref="DefaultPattern"/> and
    /// reports itself through <see cref="IsValid"/> so the settings page can say so.
    /// Results are cached by message prefix, which makes repeated extraction over a full buffer cheap.
    /// </summary>
    internal sealed class ChannelExtractor
    {
        /// <summary>Matches a leading <c>[Tag]</c> whose name is letters, digits, dot, dash, underscore or space.</summary>
        public const string DefaultPattern = @"^\[([\w.\- ]{1,64})\]";

        /// <summary>Only this many leading characters are examined, so the cache key stays small.</summary>
        public const int MaxPrefixLength = 80;

        private const int MaxCacheEntries = 4096;

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Regex _regex;

        public ChannelExtractor(string pattern = DefaultPattern)
        {
            Pattern = pattern ?? string.Empty;
            try
            {
                _regex = new Regex(Pattern, RegexOptions.CultureInvariant, MatchTimeout);
                IsValid = true;
            }
            catch (ArgumentException)
            {
                _regex = new Regex(DefaultPattern, RegexOptions.CultureInvariant, MatchTimeout);
                IsValid = false;
            }
        }

        /// <summary>The pattern as configured, even when it failed to compile.</summary>
        public string Pattern { get; }

        /// <summary>False when <see cref="Pattern"/> is not a valid regex and the default is in use.</summary>
        public bool IsValid { get; }

        /// <summary>
        /// Returns the channel name, or an empty string when the message has none. Group 1 is used when the
        /// pattern defines one, otherwise the whole match.
        /// </summary>
        public string Extract(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            string key = message.Length <= MaxPrefixLength ? message : message.Substring(0, MaxPrefixLength);
            if (_cache.TryGetValue(key, out string cached))
            {
                return cached;
            }

            string channel = string.Empty;
            try
            {
                Match match = _regex.Match(key);
                if (match.Success)
                {
                    Group group = match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1] : match.Groups[0];
                    channel = group.Value.Trim();
                }
            }
            catch (RegexMatchTimeoutException)
            {
                channel = string.Empty;
            }

            if (_cache.Count >= MaxCacheEntries)
            {
                _cache.Clear();
            }

            _cache[key] = channel;
            return channel;
        }
    }
}
