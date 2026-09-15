using System;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>How an <see cref="IgnoreRule"/> decides whether an entry is silenced.</summary>
    internal enum IgnoreMatch
    {
        /// <summary>The message is exactly the pattern.</summary>
        Message = 0,

        /// <summary>The message contains the pattern, case-insensitively.</summary>
        Contains = 1,

        /// <summary>The message matches the pattern as a regular expression.</summary>
        Regex = 2,

        /// <summary>The entry's channel is exactly the pattern.</summary>
        Channel = 3,
    }

    /// <summary>
    /// One rule for messages the user never wants to see again. Rules hide entries from the window only:
    /// capture and the journal keep them, so turning a rule off brings its entries back.
    /// </summary>
    internal sealed class IgnoreRule
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private readonly Regex _regex;

        public IgnoreRule(IgnoreMatch match, string pattern, bool enabled = true)
        {
            Match = match;
            Pattern = pattern ?? string.Empty;
            Enabled = enabled;

            if (match != IgnoreMatch.Regex || Pattern.Length == 0)
            {
                return;
            }

            try
            {
                _regex = new Regex(Pattern, RegexOptions.CultureInvariant, RegexTimeout);
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message;
            }
        }

        public IgnoreMatch Match { get; }

        public string Pattern { get; }

        public bool Enabled { get; }

        /// <summary>Why this rule cannot be used, or null when it is fine.</summary>
        public string Error { get; }

        /// <summary>A rule with no pattern, or a broken regular expression, silences nothing.</summary>
        public bool IsUsable => Enabled && Pattern.Length > 0 && Error == null;

        public bool Matches(LogEntry entry)
        {
            if (entry == null || entry.Kind == LogEntryKind.Marker || !IsUsable)
            {
                return false;
            }

            switch (Match)
            {
                case IgnoreMatch.Message:
                    return string.Equals(entry.Message, Pattern, StringComparison.Ordinal);
                case IgnoreMatch.Contains:
                    return entry.Message.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                case IgnoreMatch.Channel:
                    return string.Equals(entry.Channel, Pattern, StringComparison.Ordinal);
                case IgnoreMatch.Regex:
                    try
                    {
                        return _regex.IsMatch(entry.Message);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return false;
                    }

                default:
                    return false;
            }
        }

        /// <summary>A short description for the settings page and menus.</summary>
        public string Describe()
        {
            switch (Match)
            {
                case IgnoreMatch.Message:
                    return "Message is \"" + Pattern + "\"";
                case IgnoreMatch.Contains:
                    return "Message contains \"" + Pattern + "\"";
                case IgnoreMatch.Channel:
                    return "Channel is " + Pattern;
                default:
                    return "Message matches /" + Pattern + "/";
            }
        }
    }
}
