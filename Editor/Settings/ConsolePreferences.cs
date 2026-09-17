using System;
using UnityEditor;
using UnityEngine;

namespace ClarityConsole.Settings
{
    /// <summary>
    /// Per-user console preferences, kept in <see cref="EditorPrefs"/> rather than the project settings
    /// file: whether the console clears itself and whether it pauses on an error is a personal habit, not
    /// something a team should share through version control.
    /// </summary>
    internal static class ConsolePreferences
    {
        private const string Prefix = "ClarityConsole.";

        /// <summary>Raised after any preference changes, on the main thread.</summary>
        public static event Action Changed;

        /// <summary>Pause Play mode as soon as an error, exception or assertion is captured.</summary>
        public static bool ErrorPause
        {
            get => Get(nameof(ErrorPause), false);
            set => Set(nameof(ErrorPause), value);
        }

        /// <summary>Clear when entering Play mode.</summary>
        public static bool ClearOnPlay
        {
            get => Get(nameof(ClearOnPlay), false);
            set => Set(nameof(ClearOnPlay), value);
        }

        /// <summary>Clear when a recompile starts.</summary>
        public static bool ClearOnRecompile
        {
            get => Get(nameof(ClearOnRecompile), false);
            set => Set(nameof(ClearOnRecompile), value);
        }

        /// <summary>Clear when a player build starts.</summary>
        public static bool ClearOnBuild
        {
            get => Get(nameof(ClearOnBuild), false);
            set => Set(nameof(ClearOnBuild), value);
        }

        /// <summary>Show the Time column. Off by default so the list stays readable in a narrow panel.</summary>
        public static bool ShowTime
        {
            get => Get(nameof(ShowTime), false);
            set => Set(nameof(ShowTime), value);
        }

        /// <summary>Show the Frame column. Off by default so the list stays readable in a narrow panel.</summary>
        public static bool ShowFrame
        {
            get => Get(nameof(ShowFrame), false);
            set => Set(nameof(ShowFrame), value);
        }

        /// <summary>
        /// Show the source under every stack frame that has one, so the path an error took reads top to
        /// bottom. Off, one preview follows the selected frame.
        /// </summary>
        public static bool InlineSource
        {
            get => Get(nameof(InlineSource), true);
            set => Set(nameof(InlineSource), value);
        }

        /// <summary>List the "Domain reloaded" dividers. Off by default; Play mode markers always show.</summary>
        public static bool ShowDomainReloads
        {
            get => Get(nameof(ShowDomainReloads), false);
            set => Set(nameof(ShowDomainReloads), value);
        }

        public const int MinTextSize = 8;
        public const int MaxTextSize = 16;
        public const int DefaultTextSize = 11;

        /// <summary>
        /// Font size, in pixels, of the list rows; the detail pane uses one size less. Chips, badges and
        /// column headers keep their own small sizes.
        /// </summary>
        public static int TextSize
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(Prefix + nameof(TextSize), DefaultTextSize), MinTextSize, MaxTextSize);
            set
            {
                int clamped = Mathf.Clamp(value, MinTextSize, MaxTextSize);
                if (TextSize == clamped)
                {
                    return;
                }

                EditorPrefs.SetInt(Prefix + nameof(TextSize), clamped);
                Changed?.Invoke();
            }
        }

        /// <summary>Wrap long messages in the list so rows grow instead of cutting the text off.</summary>
        public static bool WrapMessages
        {
            get => Get(nameof(WrapMessages), false);
            set => Set(nameof(WrapMessages), value);
        }

        /// <summary>Id of the console's look. Empty means the default theme.</summary>
        public static string Theme
        {
            get => EditorPrefs.GetString(Prefix + nameof(Theme), string.Empty);
            set
            {
                string id = value ?? string.Empty;
                if (EditorPrefs.GetString(Prefix + nameof(Theme), string.Empty) == id)
                {
                    return;
                }

                EditorPrefs.SetString(Prefix + nameof(Theme), id);
                Changed?.Invoke();
            }
        }

        /// <summary>Restores every preference to its default. Used by tests and the settings page.</summary>
        public static void ResetToDefaults()
        {
            EditorPrefs.DeleteKey(Prefix + nameof(ErrorPause));
            EditorPrefs.DeleteKey(Prefix + nameof(ClearOnPlay));
            EditorPrefs.DeleteKey(Prefix + nameof(ClearOnRecompile));
            EditorPrefs.DeleteKey(Prefix + nameof(ClearOnBuild));
            EditorPrefs.DeleteKey(Prefix + nameof(ShowTime));
            EditorPrefs.DeleteKey(Prefix + nameof(ShowFrame));
            EditorPrefs.DeleteKey(Prefix + nameof(InlineSource));
            EditorPrefs.DeleteKey(Prefix + nameof(ShowDomainReloads));
            EditorPrefs.DeleteKey(Prefix + nameof(TextSize));
            EditorPrefs.DeleteKey(Prefix + nameof(WrapMessages));
            EditorPrefs.DeleteKey(Prefix + nameof(Theme));
            Changed?.Invoke();
        }

        private static bool Get(string name, bool fallback)
        {
            return EditorPrefs.GetBool(Prefix + name, fallback);
        }

        private static void Set(string name, bool value)
        {
            string key = Prefix + name;
            if (EditorPrefs.GetBool(key, false) == value && EditorPrefs.HasKey(key))
            {
                return;
            }

            EditorPrefs.SetBool(key, value);
            Changed?.Invoke();
        }
    }
}
