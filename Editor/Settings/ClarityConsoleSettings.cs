using System;
using ClarityConsole.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityConsole.Settings
{
    /// <summary>
    /// Project-wide settings, stored in <c>ProjectSettings/</c> so a team shares them through version
    /// control. Per-user preferences belong in <c>EditorPrefs</c> instead.
    /// </summary>
    [FilePath("ProjectSettings/ClarityConsole.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ClarityConsoleSettings : ScriptableSingleton<ClarityConsoleSettings>
    {
        [SerializeField]
        private string _channelPattern = ChannelExtractor.DefaultPattern;

        [SerializeField]
        private int _sourcePreviewRadius = SourceCache.DefaultRadius;

        [SerializeField]
        private bool _hideEngineFrames = true;

        [SerializeField]
        private string _hiddenFramePrefixes = string.Empty;

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

        /// <summary>The frame filter these settings describe.</summary>
        public FrameFilter CreateFrameFilter()
        {
            return new FrameFilter(HideEngineFrames, FrameFilter.ParsePrefixes(HiddenFramePrefixes));
        }

        public void ResetToDefaults()
        {
            _channelPattern = ChannelExtractor.DefaultPattern;
            _sourcePreviewRadius = SourceCache.DefaultRadius;
            _hideEngineFrames = true;
            _hiddenFramePrefixes = string.Empty;
            Persist();
        }

        private void Persist()
        {
            Save(true);
            Changed?.Invoke();
        }
    }
}
