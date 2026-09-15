using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>
    /// The set of rules that silence entries in the window. Entries are still captured and journaled, so
    /// removing a rule brings them back without losing anything.
    /// </summary>
    internal sealed class IgnoreList
    {
        public static readonly IgnoreList Empty = new IgnoreList(null);

        private readonly IgnoreRule[] _rules;

        public IgnoreList(IEnumerable<IgnoreRule> rules)
        {
            var usable = new List<IgnoreRule>();
            if (rules != null)
            {
                foreach (IgnoreRule rule in rules)
                {
                    if (rule != null && rule.IsUsable)
                    {
                        usable.Add(rule);
                    }
                }
            }

            _rules = usable.ToArray();
        }

        /// <summary>Rules that can actually silence something; broken and disabled ones are dropped.</summary>
        public IReadOnlyList<IgnoreRule> Rules => _rules;

        public bool IsEmpty => _rules.Length == 0;

        public bool ShouldIgnore(LogEntry entry)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                if (_rules[i].Matches(entry))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
