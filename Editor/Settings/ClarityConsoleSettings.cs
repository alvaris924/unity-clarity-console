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
        private bool _hideEngineFrames = true;

        [SerializeField]
        private string _hiddenFramePrefixes = string.Empty;

        [SerializeField]
        private List<IgnoreRuleSetting> _ignoreRules = new List<IgnoreRuleSetting>();

        public const int MinSourcePreviewRadius = 0;
        public const int MaxSourcePreviewRadius = 20;

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
            return new FrameFilter(HideEngineFrames, FrameFilter.ParsePrefixes(HiddenFramePrefixes));
        }

        public void ResetToDefaults()
        {
            _channelPattern = ChannelExtractor.DefaultPattern;
            _watchPattern = WatchExtractor.DefaultPattern;
            _sourcePreviewRadius = SourceCache.DefaultRadius;
            _hideEngineFrames = true;
            _hiddenFramePrefixes = string.Empty;
            _ignoreRules.Clear();
            Persist();
        }

        private void Persist()
        {
            Save(true);
            Changed?.Invoke();
        }
    }
}
