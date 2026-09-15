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

        public void ResetToDefaults()
        {
            _channelPattern = ChannelExtractor.DefaultPattern;
            _sourcePreviewRadius = SourceCache.DefaultRadius;
            Persist();
        }

        private void Persist()
        {
            Save(true);
            Changed?.Invoke();
        }
    }
}
