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
    /// The one settings page for Clarity Console, under Project Settings. The top half is "Just for you":
    /// the per-user switches the toolbar and the File menu also expose, kept in EditorPrefs and never
    /// committed. The bottom half is what the team shares through <c>ProjectSettings/ClarityConsole.asset</c>:
    /// channels, tags, watch rows, stack frames and ignore rules. Each half has its own reset.
    /// </summary>
    internal static class ClarityConsoleSettingsPage
    {
        public const string Path = ClarityConsoleSettingsProvider.Path;

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            Action unsubscribe = null;
            return new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "Clarity Console",
                keywords = new HashSet<string>
                {
                    "console", "log", "theme", "wrap", "column", "source", "stack", "chip", "channel", "tag",
                    "watch", "ignore", "fold", "frame", "preview", "clarity",
                },
                activateHandler = (_, root) => unsubscribe = Build(root),
                deactivateHandler = () =>
                {
                    unsubscribe?.Invoke();
                    unsubscribe = null;
                },
            };
        }

        /// <summary>
        /// Builds the page into <paramref name="container"/>; returns the action that detaches it from preference
        /// changes. The settings window gives a provider a plain container and never scrolls it, so the page
        /// scrolls itself: without that, everything below the fold was simply not there.
        /// </summary>
        public static Action Build(VisualElement container)
        {
            container.style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "page", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1;
            container.Add(scroll);
            VisualElement root = scroll.contentContainer;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 12;

            var refreshers = new List<Action>();

            AddSection(root, "Just for you", "Stored in EditorPrefs on this machine and never committed. The toolbar and the File menu change the same switches.", 0);
            AddTitle(root, "Look", 4);
            var theme = new DropdownField("Theme", ConsoleThemes.All.Select(t => t.DisplayName).ToList(), ThemeIndex()) { name = "theme" };
            theme.RegisterValueChangedCallback(evt =>
            {
                ConsoleTheme chosen = ConsoleThemes.All.FirstOrDefault(t => t.DisplayName == evt.newValue) ?? ConsoleThemes.Default;
                ConsolePreferences.Theme = chosen.Id;
            });
            refreshers.Add(() => theme.SetValueWithoutNotify(ConsoleThemes.All[ThemeIndex()].DisplayName));
            root.Add(theme);

            var textSize = new SliderInt("Text size", ConsolePreferences.MinTextSize, ConsolePreferences.MaxTextSize)
            {
                name = "text-size",
                value = ConsolePreferences.TextSize,
                showInputField = true,
            };
            textSize.tooltip = "Font size of the list rows in pixels; the detail pane uses one size less.";
            textSize.RegisterValueChangedCallback(evt => ConsolePreferences.TextSize = evt.newValue);
            refreshers.Add(() => textSize.SetValueWithoutNotify(ConsolePreferences.TextSize));
            root.Add(textSize);

            AddToggle(root, refreshers, "wrap", "Wrap long messages", () => ConsolePreferences.WrapMessages, v => ConsolePreferences.WrapMessages = v);
            AddToggle(root, refreshers, "chips", "Channel chips", () => ConsolePreferences.ShowChannels, v => ConsolePreferences.ShowChannels = v);
            AddHelp(root, "The row of channel chips under the toolbar. Drag its bottom edge in the console to give the chips more or less room; double-click the edge to size it to the chips again.");
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

            var reset = new Button(ConsolePreferences.ResetToDefaults)
            {
                text = "Reset my preferences",
                name = "reset",
                tooltip = "Puts the switches above back to their defaults. The shared project settings below are not touched.",
            };
            reset.style.alignSelf = Align.FlexStart;
            reset.style.marginTop = 12;
            root.Add(reset);

            AddSection(root, "Shared with the project", "Saved in ProjectSettings/ClarityConsole.asset. Commit it so the whole team gets the same channels, tags and rules.", 24);
            ClarityConsoleSettingsProvider.BuildShared(root);

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

        /// <summary>A section heading, one size up from the titles inside it, with a line saying where its values live.</summary>
        private static void AddSection(VisualElement root, string text, string where, int marginTop)
        {
            var heading = new Label(text) { name = "section" };
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 14;
            heading.style.marginTop = marginTop;
            heading.style.paddingBottom = 2;
            heading.style.borderBottomWidth = 1;
            heading.style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            root.Add(heading);

            var note = new Label(where);
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.color = new Color(0.6f, 0.6f, 0.6f);
            note.style.marginTop = 2;
            note.style.marginBottom = 2;
            root.Add(note);
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
