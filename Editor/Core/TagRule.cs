using System;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>How a <see cref="TagRule"/> decides which entries belong to its tag.</summary>
    internal enum TagMatch
    {
        /// <summary>The message contains the pattern, case-insensitively.</summary>
        Contains = 0,

        /// <summary>The message matches the pattern as a regular expression.</summary>
        Regex = 1,

        /// <summary>
        /// The entry was logged by a type whose name is the pattern: the full name, the short name
        /// without namespace, or the file name of its source, so "EnemySpawner", "Game.EnemySpawner" and
        /// "EnemySpawner.cs" all work.
        /// </summary>
        Caller = 2,
    }

    /// <summary>
    /// A tag a user made by hand: a channel assigned to entries by a match on their message or on the
    /// code that logged them, for messages that carry no <c>[Tag]</c> prefix of their own. An explicit
    /// prefix always wins over a rule.
    /// </summary>
    internal sealed class TagRule
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private readonly Regex _regex;

        public TagRule(string tag, TagMatch match, string pattern, bool enabled = true)
        {
            Tag = (tag ?? string.Empty).Trim();
            Match = match;
            Pattern = (pattern ?? string.Empty).Trim();
            Enabled = enabled;

            if (match != TagMatch.Regex || Pattern.Length == 0)
            {
                return;
            }

            try
            {
                _regex = new Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message;
            }
        }

        /// <summary>The channel entries matching this rule are put in.</summary>
        public string Tag { get; }

        public TagMatch Match { get; }

        public string Pattern { get; }

        public bool Enabled { get; }

        /// <summary>Why this rule cannot be used, or null when it is fine.</summary>
        public string Error { get; }

        /// <summary>True when the rule has a tag, a pattern and no error; rules that are not never match.</summary>
        public bool IsUsable => Tag.Length > 0 && Pattern.Length > 0 && Error == null;

        public bool Matches(LogEntry entry)
        {
            if (entry == null || !Enabled || !IsUsable)
            {
                return false;
            }

            switch (Match)
            {
                case TagMatch.Contains:
                    return entry.Message.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                case TagMatch.Regex:
                    try
                    {
                        return _regex.IsMatch(entry.Message);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return false;
                    }

                case TagMatch.Caller:
                    return CallerMatches(entry);
                default:
                    return false;
            }
        }

        /// <summary>"messages containing \"timeout\"" or "messages from EnemySpawner", for menus and lists.</summary>
        public string Describe()
        {
            switch (Match)
            {
                case TagMatch.Contains:
                    return "messages containing \"" + Pattern + "\"";
                case TagMatch.Regex:
                    return "messages matching /" + Pattern + "/";
                case TagMatch.Caller:
                    return "messages from " + Pattern;
                default:
                    return Pattern;
            }
        }

        private bool CallerMatches(LogEntry entry)
        {
            TraceFrame frame = CallerFrame.Of(entry);
            if (frame == null)
            {
                return false;
            }

            if (frame.TypeName.Length > 0)
            {
                if (string.Equals(frame.TypeName, Pattern, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(CallerFrame.ShortTypeName(frame.TypeName), Pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return frame.HasLocation && string.Equals(CallerFrame.FileName(frame.FilePath), Pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
