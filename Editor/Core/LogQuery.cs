using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClarityConsole.Core
{
    /// <summary>
    /// A parsed search query. Terms are joined by an implicit AND; <c>OR</c> joins the two terms around
    /// it; <c>-</c> excludes. Supported terms are a bare word (case-insensitive substring of the message),
    /// a <c>"quoted phrase"</c>, a <c>/regex/flags</c>, <c>sev:error,warn</c>, <c>tag:PlayFab*</c> and the
    /// modifier <c>in:stack</c>, which also searches stack traces. Parsing never throws: a malformed query
    /// matches nothing and explains itself through <see cref="Error"/>.
    /// </summary>
    internal sealed class LogQuery
    {
        public static readonly LogQuery Empty = new LogQuery(string.Empty, new List<Clause>(), null);

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private readonly List<Clause> _clauses;

        private LogQuery(string text, List<Clause> clauses, string error)
        {
            Text = text;
            _clauses = clauses;
            Error = error;
        }

        /// <summary>The query as typed.</summary>
        public string Text { get; }

        /// <summary>Human-readable reason the query cannot be used, or null when it is fine.</summary>
        public string Error { get; }

        /// <summary>True when the query has no terms, so everything matches.</summary>
        public bool IsEmpty => _clauses.Count == 0;

        /// <summary>Number of AND clauses. Test hook.</summary>
        public int ClauseCount => _clauses.Count;

        public static LogQuery Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Empty;
            }

            List<string> tokens = Tokenize(text, out string tokenError);
            if (tokenError != null)
            {
                return new LogQuery(text, new List<Clause>(), tokenError);
            }

            var clauses = new List<Clause>();
            bool orPending = false;

            foreach (string token in tokens)
            {
                if (token == "OR")
                {
                    if (clauses.Count == 0)
                    {
                        return new LogQuery(text, new List<Clause>(), "OR needs a term before it.");
                    }

                    orPending = true;
                    continue;
                }

                Term term = ParseTerm(token, out string termError);
                if (termError != null)
                {
                    return new LogQuery(text, new List<Clause>(), termError);
                }

                if (term == null)
                {
                    continue;
                }

                if (orPending)
                {
                    clauses[clauses.Count - 1].Alternatives.Add(term);
                    orPending = false;
                }
                else
                {
                    var clause = new Clause();
                    clause.Alternatives.Add(term);
                    clauses.Add(clause);
                }
            }

            if (orPending)
            {
                return new LogQuery(text, new List<Clause>(), "OR needs a term after it.");
            }

            bool searchStack = false;
            foreach (Clause clause in clauses)
            {
                foreach (Term term in clause.Alternatives)
                {
                    searchStack |= term.Kind == TermKind.SearchStack;
                }
            }

            if (searchStack)
            {
                foreach (Clause clause in clauses)
                {
                    foreach (Term term in clause.Alternatives)
                    {
                        term.SearchStack = true;
                    }
                }
            }

            clauses.RemoveAll(clause => clause.Alternatives.TrueForAll(term => term.Kind == TermKind.SearchStack));
            return new LogQuery(text, clauses, null);
        }

        public bool Matches(LogEntry entry)
        {
            if (Error != null)
            {
                return false;
            }

            for (int i = 0; i < _clauses.Count; i++)
            {
                if (!_clauses[i].Matches(entry))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Splits the query into tokens, keeping quoted phrases and regex literals whole. A leading
        /// <c>-</c> stays attached to its token.
        /// </summary>
        internal static List<string> Tokenize(string text, out string error)
        {
            var tokens = new List<string>();
            error = null;
            int i = 0;

            while (i < text.Length)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    i++;
                    continue;
                }

                int start = i;
                var token = new System.Text.StringBuilder();
                bool consumedDelimited = false;

                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    char c = text[i];
                    if (c == '"' || (c == '/' && token.Length == 0) || (c == '/' && token.Length == 1 && token[0] == '-'))
                    {
                        char delimiter = c;
                        int closing = IndexOfUnescaped(text, i + 1, delimiter);
                        if (closing < 0)
                        {
                            error = delimiter == '"'
                                ? "A quoted phrase is missing its closing quote."
                                : "A regular expression is missing its closing slash.";
                            return tokens;
                        }

                        token.Append(text, i, closing - i + 1);
                        i = closing + 1;
                        consumedDelimited = true;

                        // Regex flags follow the closing slash.
                        while (delimiter == '/' && i < text.Length && !char.IsWhiteSpace(text[i]))
                        {
                            token.Append(text[i]);
                            i++;
                        }

                        continue;
                    }

                    token.Append(c);
                    i++;
                }

                if (token.Length > 0 || consumedDelimited)
                {
                    tokens.Add(token.ToString());
                }
                else
                {
                    i = start + 1;
                }
            }

            return tokens;
        }

        private static int IndexOfUnescaped(string text, int from, char delimiter)
        {
            for (int i = from; i < text.Length; i++)
            {
                if (text[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (text[i] == delimiter)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Term ParseTerm(string token, out string error)
        {
            error = null;
            bool negate = token.Length > 1 && token[0] == '-';
            string body = negate ? token.Substring(1) : token;

            if (body.Length == 0)
            {
                return null;
            }

            if (body[0] == '/')
            {
                return ParseRegexTerm(body, negate, out error);
            }

            if (body[0] != '"')
            {
                int colon = body.IndexOf(':');
                if (colon > 0 && IsKnownField(body.Substring(0, colon)))
                {
                    return ParseFieldTerm(body.Substring(0, colon), body.Substring(colon + 1), negate, out error);
                }
            }

            string phrase = Unquote(body);
            if (phrase.Length == 0)
            {
                return null;
            }

            return new Term { Kind = TermKind.Text, Negate = negate, Text = phrase };
        }

        private static Term ParseRegexTerm(string body, bool negate, out string error)
        {
            error = null;
            int closing = body.LastIndexOf('/');
            if (closing <= 0)
            {
                error = "A regular expression is missing its closing slash.";
                return null;
            }

            string pattern = body.Substring(1, closing - 1);
            string flags = body.Substring(closing + 1);
            RegexOptions options = RegexOptions.CultureInvariant;

            foreach (char flag in flags)
            {
                switch (flag)
                {
                    case 'i':
                        options |= RegexOptions.IgnoreCase;
                        break;
                    case 'm':
                        options |= RegexOptions.Multiline;
                        break;
                    default:
                        error = "Unknown regular expression flag '" + flag + "'. Use i or m.";
                        return null;
                }
            }

            try
            {
                return new Term { Kind = TermKind.Regex, Negate = negate, Regex = new Regex(pattern, options, RegexTimeout) };
            }
            catch (ArgumentException ex)
            {
                error = "That regular expression is not valid: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Only these prefixes are read as filters, so an ordinary term that happens to contain a colon,
        /// a URL for instance, stays an ordinary text search.
        /// </summary>
        private static bool IsKnownField(string field)
        {
            switch (field.ToLowerInvariant())
            {
                case "sev":
                case "severity":
                case "tag":
                case "channel":
                case "in":
                    return true;
                default:
                    return false;
            }
        }

        private static Term ParseFieldTerm(string field, string value, bool negate, out string error)
        {
            error = null;
            value = Unquote(value);

            switch (field.ToLowerInvariant())
            {
                case "sev":
                case "severity":
                    return ParseSeverityTerm(value, negate, out error);
                case "tag":
                case "channel":
                    if (value.Length == 0)
                    {
                        error = "tag: needs a channel name, for example tag:PlayFab*.";
                        return null;
                    }

                    return new Term { Kind = TermKind.Channel, Negate = negate, Regex = GlobToRegex(value) };
                case "in":
                    if (!string.Equals(value, "stack", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "in: only supports 'stack'.";
                        return null;
                    }

                    return new Term { Kind = TermKind.SearchStack };
                default:
                    error = "Unknown filter '" + field + ":'. Use sev:, tag: or in:stack.";
                    return null;
            }
        }

        private static Term ParseSeverityTerm(string value, bool negate, out string error)
        {
            error = null;
            var severities = new HashSet<LogSeverity>();

            foreach (string name in value.Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                switch (trimmed.ToLowerInvariant())
                {
                    case "log":
                    case "info":
                        severities.Add(LogSeverity.Log);
                        break;
                    case "warn":
                    case "warning":
                        severities.Add(LogSeverity.Warning);
                        break;
                    case "error":
                        severities.Add(LogSeverity.Error);
                        break;
                    case "exception":
                        severities.Add(LogSeverity.Exception);
                        break;
                    case "assert":
                        severities.Add(LogSeverity.Assert);
                        break;
                    default:
                        error = "Unknown severity '" + trimmed + "'. Use log, warn, error, exception or assert.";
                        return null;
                }
            }

            if (severities.Count == 0)
            {
                error = "sev: needs at least one severity, for example sev:error,warn.";
                return null;
            }

            return new Term { Kind = TermKind.Severity, Negate = negate, Severities = severities };
        }

        private static Regex GlobToRegex(string glob)
        {
            var pattern = new System.Text.StringBuilder("^");
            foreach (char c in glob)
            {
                if (c == '*')
                {
                    pattern.Append(".*");
                }
                else if (c == '?')
                {
                    pattern.Append('.');
                }
                else
                {
                    pattern.Append(Regex.Escape(c.ToString()));
                }
            }

            pattern.Append('$');
            return new Regex(pattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2).Replace("\\\"", "\"");
            }

            return value;
        }

        private enum TermKind
        {
            Text,
            Regex,
            Severity,
            Channel,
            SearchStack,
        }

        private sealed class Clause
        {
            public List<Term> Alternatives { get; } = new List<Term>();

            public bool Matches(LogEntry entry)
            {
                for (int i = 0; i < Alternatives.Count; i++)
                {
                    if (Alternatives[i].Matches(entry))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class Term
        {
            public TermKind Kind;
            public bool Negate;
            public bool SearchStack;
            public string Text;
            public Regex Regex;
            public HashSet<LogSeverity> Severities;

            public bool Matches(LogEntry entry)
            {
                bool matched;
                switch (Kind)
                {
                    case TermKind.Text:
                        matched = entry.Message.IndexOf(Text, StringComparison.OrdinalIgnoreCase) >= 0
                            || (SearchStack && entry.StackTrace.IndexOf(Text, StringComparison.OrdinalIgnoreCase) >= 0);
                        break;
                    case TermKind.Regex:
                        matched = SafeIsMatch(entry.Message) || (SearchStack && SafeIsMatch(entry.StackTrace));
                        break;
                    case TermKind.Severity:
                        matched = Severities.Contains(entry.Severity);
                        break;
                    case TermKind.Channel:
                        matched = entry.Channel.Length > 0 && SafeIsMatch(entry.Channel);
                        break;
                    default:
                        return true;
                }

                return Negate ? !matched : matched;
            }

            private bool SafeIsMatch(string input)
            {
                try
                {
                    return Regex.IsMatch(input);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }
        }
    }
}
