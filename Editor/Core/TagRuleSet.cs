using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>The tag rules in the order they were made; the first one that matches an entry names its channel.</summary>
    internal sealed class TagRuleSet
    {
        public static readonly TagRuleSet Empty = new TagRuleSet(null);

        private readonly TagRule[] _rules;

        public TagRuleSet(IEnumerable<TagRule> rules)
        {
            var kept = new List<TagRule>();
            if (rules != null)
            {
                foreach (TagRule rule in rules)
                {
                    if (rule != null)
                    {
                        kept.Add(rule);
                    }
                }
            }

            _rules = kept.ToArray();
        }

        public IReadOnlyList<TagRule> Rules => _rules;

        public bool IsEmpty => _rules.Length == 0;

        /// <summary>The tag of the first enabled rule that matches, or null when none does.</summary>
        public string Resolve(LogEntry entry)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                if (_rules[i].Matches(entry))
                {
                    return _rules[i].Tag;
                }
            }

            return null;
        }
    }
}
