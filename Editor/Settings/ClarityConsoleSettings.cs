using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityConsole.Settings
{
    /// <summary>
    /// Project-wide settings, stored in <c>ProjectSettings/</c> so a team shares them through version
    /// control. Per-user preferences belong in <c>EditorPrefs</c> instead.
    /// </summary>
    /// <summary>One serialized ignore rule. Kept simple so the settings file stays readable in review.</summary>
    [Serializable]
    internal sealed class IgnoreRuleSetting
    {
        public IgnoreMatch match;
        public string pattern;
        public bool enabled = true;
    }

    /// <summary>One serialized tag rule; a hand-made channel.</summary>
    [Serializable]
    internal sealed class TagRuleSetting
    {
        public string tag;
        public TagMatch match;
        public string pattern;
        public bool enabled = true;
    }

    [FilePath("ProjectSettings/ClarityConsole.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ClarityConsoleSettings : ScriptableSingleton<ClarityConsoleSettings>
    {
        [SerializeField]
        private string _channelPattern = ChannelExtractor.DefaultPattern;

        [SerializeField]
        private string _watchPattern = WatchExtractor.DefaultPattern;

        [SerializeField]
        private int _sourcePreviewRadius = SourceCache.DefaultRadius;

        [SerializeField]
        private int _sourceHoverRadius = DefaultSourceHoverRadius;

        [SerializeField]
        private bool _hideEngineFrames;

        [SerializeField]
        private bool _hidePackageFrames;

        [SerializeField]
        private string _hiddenFramePrefixes = string.Empty;

        [SerializeField]
        private List<IgnoreRuleSetting> _ignoreRules = new List<IgnoreRuleSetting>();

        [SerializeField]
        private List<TagRuleSetting> _tagRules = new List<TagRuleSetting>();

        [SerializeField]
        private bool _autoTagByCaller;

        public const int MinSourcePreviewRadius = 0;
        public const int MaxSourcePreviewRadius = 20;
        public const int DefaultSourceHoverRadius = 7;
        public const int MaxSourceHoverRadius = 40;

        /// <summary>Raised after any setting changes, on the main thread.</summary>
        public static event Action Changed;

        /// <summary>Regex whose first group names the channel of a message. See <see cref="ChannelExtractor"/>.</summary>
        public string ChannelPattern
        {
            get => string.IsNullOrEmpty(_channelPattern) ? ChannelExtractor.DefaultPattern : _channelPattern;
            set
            {
                string pattern = value ?? string.Empty;
                if (_channelPattern == pattern)
                {
                    return;
                }

                _channelPattern = pattern;
                Persist();
            }
        }

        /// <summary>
        /// How many source lines the hover card shows on each side of a frame's line. Larger than the
        /// inline preview, since the card is asked for on purpose and goes away on its own.
        /// </summary>
        public int SourceHoverRadius
        {
            get => Mathf.Clamp(_sourceHoverRadius, MinSourcePreviewRadius, MaxSourceHoverRadius);
            set
            {
                int clamped = Mathf.Clamp(value, MinSourcePreviewRadius, MaxSourceHoverRadius);
                if (_sourceHoverRadius == clamped)
                {
                    return;
                }

                _sourceHoverRadius = clamped;
                Persist();
            }
        }

        /// <summary>How many source lines to show on each side of the line a stack frame points at.</summary>
        public int SourcePreviewRadius
        {
            get => Mathf.Clamp(_sourcePreviewRadius, MinSourcePreviewRadius, MaxSourcePreviewRadius);
            set
            {
                int clamped = Mathf.Clamp(value, MinSourcePreviewRadius, MaxSourcePreviewRadius);
                if (_sourcePreviewRadius == clamped)
                {
                    return;
                }

                _sourcePreviewRadius = clamped;
                Persist();
            }
        }

        /// <summary>Fold frames from the engine, the Editor, the runtime and the capture path.</summary>
        /// <summary>Fold frames compiled from installed packages, the ones under Library/PackageCache.</summary>
        public bool HidePackageFrames
        {
            get => _hidePackageFrames;
            set
            {
                if (_hidePackageFrames == value)
                {
                    return;
                }

                _hidePackageFrames = value;
                Persist();
            }
        }

        public bool HideEngineFrames
        {
            get => _hideEngineFrames;
            set
            {
                if (_hideEngineFrames == value)
                {
                    return;
                }

                _hideEngineFrames = value;
                Persist();
            }
        }

        /// <summary>Type-name prefixes to fold away, one per line. Lines starting with # are comments.</summary>
        public string HiddenFramePrefixes
        {
            get => _hiddenFramePrefixes ?? string.Empty;
            set
            {
                string text = value ?? string.Empty;
                if (_hiddenFramePrefixes == text)
                {
                    return;
                }

                _hiddenFramePrefixes = text;
                Persist();
            }
        }

        /// <summary>
        /// Regex whose first group names a watch key. Entries sharing a key replace each other in the
        /// window instead of piling up. Empty turns watch rows off.
        /// </summary>
        public string WatchPattern
        {
            get => _watchPattern ?? string.Empty;
            set
            {
                string pattern = value ?? string.Empty;
                if (_watchPattern == pattern)
                {
                    return;
                }

                _watchPattern = pattern;
                Persist();
            }
        }

        /// <summary>The watch extractor these settings describe, or null when watch rows are off.</summary>
        public WatchExtractor CreateWatchExtractor()
        {
            return WatchPattern.Length == 0 ? null : new WatchExtractor(WatchPattern);
        }

        /// <summary>Rules that silence entries in the window. Edit through the methods below so the change is saved.</summary>
        public IReadOnlyList<IgnoreRuleSetting> IgnoreRules => _ignoreRules;

        /// <summary>Adds a rule unless an identical one is already there. Returns true when it was added.</summary>
        public bool AddIgnoreRule(IgnoreMatch match, string pattern)
        {
            pattern = pattern ?? string.Empty;
            if (pattern.Length == 0)
            {
                return false;
            }

            foreach (IgnoreRuleSetting existing in _ignoreRules)
            {
                if (existing.match == match && existing.pattern == pattern)
                {
                    return false;
                }
            }

            _ignoreRules.Add(new IgnoreRuleSetting { match = match, pattern = pattern, enabled = true });
            Persist();
            return true;
        }

        public void RemoveIgnoreRule(int index)
        {
            if (index < 0 || index >= _ignoreRules.Count)
            {
                return;
            }

            _ignoreRules.RemoveAt(index);
            Persist();
        }

        public void SetIgnoreRuleEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= _ignoreRules.Count || _ignoreRules[index].enabled == enabled)
            {
                return;
            }

            _ignoreRules[index].enabled = enabled;
            Persist();
        }

        public void ClearIgnoreRules()
        {
            if (_ignoreRules.Count == 0)
            {
                return;
            }

            _ignoreRules.Clear();
            Persist();
        }

        /// <summary>Hand-made tags. Edit through the methods below so the change is saved.</summary>
        public IReadOnlyList<TagRuleSetting> TagRules => _tagRules;

        /// <summary>Tag entries with no prefix and no matching rule with the type that logged them.</summary>
        public bool AutoTagByCaller
        {
            get => _autoTagByCaller;
            set
            {
                if (_autoTagByCaller == value)
                {
                    return;
                }

                _autoTagByCaller = value;
                Persist();
            }
        }

        /// <summary>Adds a tag rule unless an identical one exists. Returns true when it was added.</summary>
        public bool AddTagRule(string tag, TagMatch match, string pattern)
        {
            tag = (tag ?? string.Empty).Trim();
            pattern = (pattern ?? string.Empty).Trim();
            if (tag.Length == 0 || pattern.Length == 0)
            {
                return false;
            }

            foreach (TagRuleSetting existing in _tagRules)
            {
                if (existing.match == match && existing.pattern == pattern && existing.tag == tag)
                {
                    return false;
                }
            }

            _tagRules.Add(new TagRuleSetting { tag = tag, match = match, pattern = pattern, enabled = true });
            Persist();
            return true;
        }

        public void RemoveTagRule(int index)
        {
            if (index < 0 || index >= _tagRules.Count)
            {
                return;
            }

            _tagRules.RemoveAt(index);
            Persist();
        }

        public void SetTagRuleEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= _tagRules.Count || _tagRules[index].enabled == enabled)
            {
                return;
            }

            _tagRules[index].enabled = enabled;
            Persist();
        }

        public void ClearTagRules()
        {
            if (_tagRules.Count == 0)
            {
                return;
            }

            _tagRules.Clear();
            Persist();
        }

        /// <summary>The tag rules these settings describe.</summary>
        public TagRuleSet CreateTagRules()
        {
            var rules = new List<TagRule>(_tagRules.Count);
            foreach (TagRuleSetting setting in _tagRules)
            {
                rules.Add(new TagRule(setting.tag, setting.match, setting.pattern, setting.enabled));
            }

            return new TagRuleSet(rules);
        }

        /// <summary>The ignore list these settings describe.</summary>
        public IgnoreList CreateIgnoreList()
        {
            var rules = new List<IgnoreRule>(_ignoreRules.Count);
            foreach (IgnoreRuleSetting setting in _ignoreRules)
            {
                rules.Add(new IgnoreRule(setting.match, setting.pattern, setting.enabled));
            }

            return new IgnoreList(rules);
        }

        /// <summary>The frame filter these settings describe.</summary>
        public FrameFilter CreateFrameFilter()
        {
            return new FrameFilter(HideEngineFrames, HidePackageFrames, FrameFilter.ParsePrefixes(HiddenFramePrefixes));
        }

        public void ResetToDefaults()
        {
            _channelPattern = ChannelExtractor.DefaultPattern;
            _watchPattern = WatchExtractor.DefaultPattern;
            _sourcePreviewRadius = SourceCache.DefaultRadius;
            _sourceHoverRadius = DefaultSourceHoverRadius;
            _hideEngineFrames = false;
            _hidePackageFrames = false;
            _hiddenFramePrefixes = string.Empty;
            _ignoreRules.Clear();
            _tagRules.Clear();
            _autoTagByCaller = false;
            Persist();
        }

        private void Persist()
        {
            Save(true);
            Changed?.Invoke();
        }
    }
}
