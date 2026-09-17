using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The Preferences page for Clarity Console: the per-user switches that the toolbar and the File menu
    /// also expose, in one place, plus a reset. Project-wide settings live on the Project Settings page.
    /// </summary>
    internal static class ClarityConsolePreferencesProvider
    {
        public const string Path = "Preferences/Clarity Console";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            Action unsubscribe = null;
            return new SettingsProvider(Path, SettingsScope.User)
            {
                label = "Clarity Console",
                keywords = new HashSet<string> { "console", "log", "theme", "wrap", "column", "source", "stack", "clarity" },
                activateHandler = (_, root) => unsubscribe = Build(root),
                deactivateHandler = () =>
                {
                    unsubscribe?.Invoke();
                    unsubscribe = null;
                },
            };
        }

        /// <summary>Builds the page into <paramref name="root"/>; returns the action that detaches it from preference changes.</summary>
        public static Action Build(VisualElement root)
        {
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            var refreshers = new List<Action>();

            AddTitle(root, "Look", 0);
            var theme = new DropdownField("Theme", ConsoleThemes.All.Select(t => t.DisplayName).ToList(), ThemeIndex()) { name = "theme" };
            theme.RegisterValueChangedCallback(evt =>
            {
                ConsoleTheme chosen = ConsoleThemes.All.FirstOrDefault(t => t.DisplayName == evt.newValue) ?? ConsoleThemes.Default;
                ConsolePreferences.Theme = chosen.Id;
            });
            refreshers.Add(() => theme.SetValueWithoutNotify(ConsoleThemes.All[ThemeIndex()].DisplayName));
            root.Add(theme);

            AddToggle(root, refreshers, "wrap", "Wrap long messages", () => ConsolePreferences.WrapMessages, v => ConsolePreferences.WrapMessages = v);
            AddToggle(root, refreshers, "inline-source", "Source under every frame", () => ConsolePreferences.InlineSource, v => ConsolePreferences.InlineSource = v);
            AddHelp(root, "On, the detail pane shows a few lines of code under each stack frame in order, so the path an error took reads top to bottom. Off, one preview follows the frame you click.");

            AddTitle(root, "Columns", 12);
            AddHelp(root, "Time and Frame are off by default so a narrow panel is mostly message. Right-clicking the list header toggles them as well.");
            AddToggle(root, refreshers, "time", "Time", () => ConsolePreferences.ShowTime, v => ConsolePreferences.ShowTime = v);
            AddToggle(root, refreshers, "frame", "Frame", () => ConsolePreferences.ShowFrame, v => ConsolePreferences.ShowFrame = v);

            AddTitle(root, "Behaviour", 12);
            AddToggle(root, refreshers, "domain-reloads", "Show domain reloads", () => ConsolePreferences.ShowDomainReloads, v => ConsolePreferences.ShowDomainReloads = v);
            AddHelp(root, "The \"Domain reloaded\" divider after every recompile is hidden by default; Play mode and Editor start markers always show.");
            AddToggle(root, refreshers, "error-pause", "Error Pause", () => ConsolePreferences.ErrorPause, v => ConsolePreferences.ErrorPause = v);
            AddToggle(root, refreshers, "clear-on-play", "Clear on Play", () => ConsolePreferences.ClearOnPlay, v => ConsolePreferences.ClearOnPlay = v);
            AddToggle(root, refreshers, "clear-on-recompile", "Clear on Recompile", () => ConsolePreferences.ClearOnRecompile, v => ConsolePreferences.ClearOnRecompile = v);
            AddToggle(root, refreshers, "clear-on-build", "Clear on Build", () => ConsolePreferences.ClearOnBuild, v => ConsolePreferences.ClearOnBuild = v);

            var reset = new Button(ConsolePreferences.ResetToDefaults) { text = "Reset to defaults", name = "reset" };
            reset.style.alignSelf = Align.FlexStart;
            reset.style.marginTop = 14;
            root.Add(reset);

            // Keep the page honest when a toolbar toggle or another window changes a preference.
            void Refresh()
            {
                foreach (Action refresh in refreshers)
                {
                    refresh();
                }
            }

            ConsolePreferences.Changed += Refresh;
            return () => ConsolePreferences.Changed -= Refresh;
        }

        private static int ThemeIndex()
        {
            ConsoleTheme current = ConsoleThemes.Find(ConsolePreferences.Theme);
            for (int i = 0; i < ConsoleThemes.All.Count; i++)
            {
                if (ReferenceEquals(ConsoleThemes.All[i], current))
                {
                    return i;
                }
            }

            return 0;
        }

        private static void AddTitle(VisualElement root, string text, int marginTop)
        {
            var title = new Label(text);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = marginTop;
            title.style.marginBottom = 4;
            root.Add(title);
        }

        private static void AddHelp(VisualElement root, string text)
        {
            var help = new Label(text);
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginBottom = 4;
            root.Add(help);
        }

        private static void AddToggle(VisualElement root, List<Action> refreshers, string name, string label, Func<bool> get, Action<bool> set)
        {
            var toggle = new Toggle(label) { name = name, value = get() };
            toggle.RegisterValueChangedCallback(evt => set(evt.newValue));
            refreshers.Add(() => toggle.SetValueWithoutNotify(get()));
            root.Add(toggle);
        }
    }
}
