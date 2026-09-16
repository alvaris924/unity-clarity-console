using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>A named look for the console: one stylesheet that defines the <c>--cc-*</c> variables.</summary>
    internal sealed class ConsoleTheme
    {
        public ConsoleTheme(string id, string displayName, string styleSheetPath)
        {
            Id = id;
            DisplayName = displayName;
            StyleSheetPath = styleSheetPath;
        }

        /// <summary>Stable key stored in preferences.</summary>
        public string Id { get; }

        public string DisplayName { get; }

        /// <summary>Asset path of the theme's stylesheet.</summary>
        public string StyleSheetPath { get; }

        public StyleSheet Load()
        {
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
        }
    }

    /// <summary>The themes that ship with the package. The first one is the default.</summary>
    internal static class ConsoleThemes
    {
        private const string Folder = "Packages/com.alvaris.clarity-console/Editor/UI/Themes/";

        public static readonly ConsoleTheme Native = new ConsoleTheme("native", "Native", Folder + "Native.uss");
        public static readonly ConsoleTheme Obsidian = new ConsoleTheme("obsidian", "Obsidian", Folder + "Obsidian.uss");
        public static readonly ConsoleTheme Paper = new ConsoleTheme("paper", "Paper", Folder + "Paper.uss");
        public static readonly ConsoleTheme SciFi = new ConsoleTheme("scifi", "Sci-fi", Folder + "SciFi.uss");

        public static readonly IReadOnlyList<ConsoleTheme> All = new[] { Native, Obsidian, Paper, SciFi };

        public static ConsoleTheme Default => Native;

        /// <summary>The theme with this id, or the default when the id is unknown or empty.</summary>
        public static ConsoleTheme Find(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (ConsoleTheme theme in All)
                {
                    if (string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return theme;
                    }
                }
            }

            return Default;
        }
    }
}
