using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEditor;
using UnityEngine.UIElements;

namespace ClarityConsole.Settings
{
    /// <summary>The Project Settings page for Clarity Console.</summary>
    internal static class ClarityConsoleSettingsProvider
    {
        private const string WatchExample = "[watch:PlayerHP] 87";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Clarity Console", SettingsScope.Project)
            {
                label = "Clarity Console",
                keywords = new HashSet<string> { "console", "log", "channel", "tag", "clarity" },
                activateHandler = (_, root) => Build(root),
            };
        }

        /// <summary>
        /// Builds the page. The settings window gives providers a plain container and does not scroll it, so
        /// the content goes into a ScrollView of its own: a page taller than the window used to lose its tail,
        /// the Tags section first of all. Internal for the tests.
        /// </summary>
        internal static void Build(VisualElement root)
        {
            root.style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "page", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            VisualElement page = scroll.contentContainer;
            page.style.paddingLeft = 10;
            page.style.paddingRight = 10;
            page.style.paddingTop = 8;
            page.style.paddingBottom = 12;

            var title = new Label("Channels");
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.marginBottom = 4;
            page.Add(title);

            var help = new Label(
                "Messages that start with a tag are grouped into channels you can filter by. " +
                "The first capture group names the channel; only the first " + ChannelExtractor.MaxPrefixLength +
                " characters of a message are examined.");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginBottom = 6;
            page.Add(help);

            var status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = 4;

            var field = new TextField("Channel pattern") { value = ClarityConsoleSettings.instance.ChannelPattern };
            field.RegisterValueChangedCallback(evt =>
            {
                ClarityConsoleSettings.instance.ChannelPattern = evt.newValue;
                UpdateStatus(status, evt.newValue);
            });
            page.Add(field);
            page.Add(status);
            UpdateStatus(status, field.value);

            var tagsTitle = new Label("Tags");
            tagsTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            tagsTitle.style.marginTop = 12;
            tagsTitle.style.marginBottom = 4;
            page.Add(tagsTitle);

            var tagsHelp = new Label(
                "Messages without a [Tag] prefix can still get a channel. A rule tags messages that contain " +
                "some text, match a regular expression, or come from a given class or file; an explicit prefix " +
                "always wins. Right-click a row in the console for a prompt filled in from that entry.");
            tagsHelp.style.whiteSpace = WhiteSpace.Normal;
            tagsHelp.style.marginBottom = 4;
            page.Add(tagsHelp);

            var autoTag = new Toggle("Tag by caller when nothing else applies")
            {
                value = ClarityConsoleSettings.instance.AutoTagByCaller,
            };
            autoTag.tooltip = "Entries with no prefix and no matching rule are tagged with the short name of the class that logged them.";
            autoTag.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.AutoTagByCaller = evt.newValue);
            autoTag.style.marginBottom = 6;
            page.Add(autoTag);

            var tagRules = new VisualElement();
            tagRules.style.marginBottom = 8;
            var tagForm = BuildTagForm(() => RebuildTagRules(tagRules));
            page.Add(tagForm);
            page.Add(tagRules);
            RebuildTagRules(tagRules);

            var watchTitle = new Label("Watch rows");
            watchTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            watchTitle.style.marginTop = 12;
            watchTitle.style.marginBottom = 4;
            page.Add(watchTitle);

            var watchHelp = new Label(
                "Messages that start with a watch key, " + WatchExample + " by default, replace each other " +
                "in the window instead of piling up, so a value you log every frame reads as one line that " +
                "changes. Leave the pattern empty to turn this off.");
            watchHelp.style.whiteSpace = WhiteSpace.Normal;
            watchHelp.style.marginBottom = 4;
            page.Add(watchHelp);

            var watch = new TextField("Watch pattern") { value = ClarityConsoleSettings.instance.WatchPattern };
            watch.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.WatchPattern = evt.newValue);
            page.Add(watch);

            var frames = new Label("Stack frames");
            frames.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            frames.style.marginTop = 12;
            frames.style.marginBottom = 4;
            page.Add(frames);

            var radius = new SliderInt("Preview lines", ClarityConsoleSettings.MinSourcePreviewRadius, ClarityConsoleSettings.MaxSourcePreviewRadius)
            {
                value = ClarityConsoleSettings.instance.SourcePreviewRadius,
                showInputField = true,
            };
            radius.tooltip = "Lines of source shown on each side of the line a stack frame points at. Zero shows only that line.";
            radius.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.SourcePreviewRadius = evt.newValue);
            page.Add(radius);

            var hoverRadius = new SliderInt("Hover card lines", ClarityConsoleSettings.MinSourcePreviewRadius, ClarityConsoleSettings.MaxSourceHoverRadius)
            {
                value = ClarityConsoleSettings.instance.SourceHoverRadius,
                showInputField = true,
            };
            hoverRadius.tooltip = "Lines of source shown on each side of the frame's line in the card that appears while the pointer rests on a frame or its source block.";
            hoverRadius.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.SourceHoverRadius = evt.newValue);
            page.Add(hoverRadius);

            var hideEngine = new Toggle("Fold engine frames")
            {
                value = ClarityConsoleSettings.instance.HideEngineFrames,
            };
            hideEngine.tooltip = "Fold frames from the engine, the Editor, the runtime and Unity's own packages into one row, so your own code is what you see first. Off by default: every frame shows, as in the stock console.";
            hideEngine.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.HideEngineFrames = evt.newValue);
            page.Add(hideEngine);

            var hidePackages = new Toggle("Fold package frames")
            {
                value = ClarityConsoleSettings.instance.HidePackageFrames,
            };
            hidePackages.tooltip = "Fold frames compiled from installed packages, the ones under Library/PackageCache, such as a package's own logging path. Your embedded and local packages are not affected.";
            hidePackages.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.HidePackageFrames = evt.newValue);
            page.Add(hidePackages);

            var prefixes = new TextField("Also fold types starting with") { multiline = true, value = ClarityConsoleSettings.instance.HiddenFramePrefixes };
            prefixes.tooltip = "One type-name prefix per line, for example Cysharp.Threading.Tasks. Lines starting with # are comments.";
            prefixes.style.minHeight = 54;
            prefixes.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.HiddenFramePrefixes = evt.newValue);
            page.Add(prefixes);

            var ignoreTitle = new Label("Ignored messages");
            ignoreTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            ignoreTitle.style.marginTop = 12;
            ignoreTitle.style.marginBottom = 4;
            page.Add(ignoreTitle);

            var ignoreHelp = new Label(
                "Entries matching these rules are hidden from the console window. They are still captured " +
                "and journaled, so removing a rule brings them back. Add rules by right-clicking a row in the console.");
            ignoreHelp.style.whiteSpace = WhiteSpace.Normal;
            ignoreHelp.style.marginBottom = 4;
            page.Add(ignoreHelp);

            var rules = new VisualElement();
            page.Add(rules);
            RebuildRules(rules);

            var preferences = new Button(() => SettingsService.OpenUserPreferences("Preferences/Clarity Console"))
            {
                text = "Open Preferences…",
                name = "preferences",
                tooltip = "Per-user options: theme, text size, channel chips, columns, Error Pause and the clear-on switches.",
            };
            preferences.style.alignSelf = Align.FlexStart;
            preferences.style.marginTop = 12;
            page.Add(preferences);

            var reset = new Button(() =>
            {
                ClarityConsoleSettings.instance.ResetToDefaults();
                field.SetValueWithoutNotify(ClarityConsoleSettings.instance.ChannelPattern);
                watch.SetValueWithoutNotify(ClarityConsoleSettings.instance.WatchPattern);
                radius.SetValueWithoutNotify(ClarityConsoleSettings.instance.SourcePreviewRadius);
                hoverRadius.SetValueWithoutNotify(ClarityConsoleSettings.instance.SourceHoverRadius);
                hideEngine.SetValueWithoutNotify(ClarityConsoleSettings.instance.HideEngineFrames);
                hidePackages.SetValueWithoutNotify(ClarityConsoleSettings.instance.HidePackageFrames);
                prefixes.SetValueWithoutNotify(ClarityConsoleSettings.instance.HiddenFramePrefixes);
                autoTag.SetValueWithoutNotify(ClarityConsoleSettings.instance.AutoTagByCaller);
                UpdateStatus(status, field.value);
                RebuildTagRules(tagRules);
                RebuildRules(rules);
            })
            {
                text = "Reset to default",
            };
            reset.style.marginTop = 8;
            reset.style.width = 140;
            page.Add(reset);
        }

        private const int FieldHeight = 20;

        /// <summary>Tag name, match kind and pattern on one line, with an Add button.</summary>
        private static VisualElement BuildTagForm(Action onAdded)
        {
            var form = new VisualElement();
            form.style.flexDirection = FlexDirection.Row;
            form.style.alignItems = Align.Center;
            form.style.height = FieldHeight + 2;
            form.style.marginTop = 4;
            form.style.marginBottom = 6;
            form.style.marginRight = 8;

            // Fields in a horizontal row take their height from the row, which has none of its own, so
            // each gets the standard line height explicitly.
            var tag = new TextField { name = "tag-name" };
            tag.style.width = 110;
            tag.style.height = FieldHeight;
            tag.tooltip = "The channel name, as it will read on the chip.";
            SetPlaceholder(tag, "Tag");
            form.Add(tag);

            var match = new EnumField(TagMatch.Contains) { name = "tag-match" };
            match.style.width = 90;
            match.style.height = FieldHeight;
            match.style.marginLeft = 4;
            form.Add(match);

            var pattern = new TextField { name = "tag-pattern" };
            pattern.style.flexGrow = 1;
            pattern.style.flexShrink = 1;
            pattern.style.flexBasis = 0;
            pattern.style.minWidth = 120;
            pattern.style.height = FieldHeight;
            pattern.style.marginLeft = 4;
            pattern.tooltip = "Text the message contains, a regular expression, or a class or file name such as EnemySpawner or EnemySpawner.cs.";
            SetPlaceholder(pattern, "Pattern");
            form.Add(pattern);

            var add = new Button(() =>
            {
                if (ClarityConsoleSettings.instance.AddTagRule(tag.value, (TagMatch)match.value, pattern.value))
                {
                    tag.SetValueWithoutNotify(string.Empty);
                    pattern.SetValueWithoutNotify(string.Empty);
                    onAdded();
                }
            })
            {
                text = "Add",
                name = "tag-add",
            };
            add.style.marginLeft = 4;
            add.style.flexShrink = 0;
            add.style.height = FieldHeight;
            add.style.minWidth = 52;
            form.Add(add);
            return form;
        }

        private static void SetPlaceholder(TextField field, string placeholder)
        {
#if UNITY_2023_1_OR_NEWER
            field.textEdition.placeholder = placeholder;
#else
            field.tooltip = string.IsNullOrEmpty(field.tooltip) ? placeholder : field.tooltip;
#endif
        }

        /// <summary>Redraws the tag rule rows.</summary>
        private static void RebuildTagRules(VisualElement container)
        {
            container.Clear();
            IReadOnlyList<TagRuleSetting> settings = ClarityConsoleSettings.instance.TagRules;

            if (settings.Count == 0)
            {
                var none = new Label("No tags yet.");
                none.style.color = new UnityEngine.Color(0.6f, 0.6f, 0.6f);
                container.Add(none);
                return;
            }

            for (int i = 0; i < settings.Count; i++)
            {
                int index = i;
                TagRuleSetting setting = settings[i];
                var rule = new TagRule(setting.tag, setting.match, setting.pattern, setting.enabled);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 2;

                var enabled = new Toggle { value = setting.enabled };
                enabled.tooltip = "Turn the tag off to drop its channel without deleting the rule.";
                enabled.RegisterValueChangedCallback(evt =>
                {
                    ClarityConsoleSettings.instance.SetTagRuleEnabled(index, evt.newValue);
                    RebuildTagRules(container);
                });
                row.Add(enabled);

                var description = new Label(rule.Tag + "  ←  " + rule.Describe());
                description.style.flexGrow = 1;
                description.style.overflow = Overflow.Hidden;
                if (rule.Error != null)
                {
                    description.text += "  —  " + rule.Error;
                    description.style.color = new UnityEngine.Color(0.9f, 0.4f, 0.4f);
                }
                else if (!setting.enabled)
                {
                    description.style.color = new UnityEngine.Color(0.6f, 0.6f, 0.6f);
                }

                row.Add(description);

                var remove = new Button(() =>
                {
                    ClarityConsoleSettings.instance.RemoveTagRule(index);
                    RebuildTagRules(container);
                })
                {
                    text = "Remove",
                };
                row.Add(remove);
                container.Add(row);
            }
        }

        /// <summary>Redraws the rule rows. Cheap: there are only ever a handful of rules.</summary>
        private static void RebuildRules(VisualElement container)
        {
            container.Clear();
            IReadOnlyList<IgnoreRuleSetting> settings = ClarityConsoleSettings.instance.IgnoreRules;

            if (settings.Count == 0)
            {
                var none = new Label("No rules yet.");
                none.style.color = new UnityEngine.Color(0.6f, 0.6f, 0.6f);
                container.Add(none);
                return;
            }

            for (int i = 0; i < settings.Count; i++)
            {
                int index = i;
                IgnoreRuleSetting setting = settings[i];
                var rule = new IgnoreRule(setting.match, setting.pattern, setting.enabled);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 2;

                var enabled = new Toggle { value = setting.enabled };
                enabled.tooltip = "Turn the rule off to see its entries again without deleting it.";
                enabled.RegisterValueChangedCallback(evt =>
                {
                    ClarityConsoleSettings.instance.SetIgnoreRuleEnabled(index, evt.newValue);
                    RebuildRules(container);
                });
                row.Add(enabled);

                var description = new Label(rule.Describe());
                description.style.flexGrow = 1;
                description.style.overflow = Overflow.Hidden;
                if (rule.Error != null)
                {
                    description.text += "  —  " + rule.Error;
                    description.style.color = new UnityEngine.Color(0.9f, 0.4f, 0.4f);
                }
                else if (!setting.enabled)
                {
                    description.style.color = new UnityEngine.Color(0.6f, 0.6f, 0.6f);
                }

                row.Add(description);

                var remove = new Button(() =>
                {
                    ClarityConsoleSettings.instance.RemoveIgnoreRule(index);
                    RebuildRules(container);
                })
                {
                    text = "Remove",
                };
                remove.style.width = 70;
                row.Add(remove);

                container.Add(row);
            }

            var clear = new Button(() =>
            {
                ClarityConsoleSettings.instance.ClearIgnoreRules();
                RebuildRules(container);
            })
            {
                text = "Remove all",
            };
            clear.style.width = 90;
            clear.style.marginTop = 4;
            container.Add(clear);
        }

        private static void UpdateStatus(Label status, string pattern)
        {
            var extractor = new ChannelExtractor(pattern);
            if (!extractor.IsValid)
            {
                status.text = "That is not a valid regular expression, so the default pattern is in use.";
                status.style.color = new UnityEngine.Color(0.9f, 0.4f, 0.4f);
                return;
            }

            string sample = extractor.Extract("[PlayFabCBSManager] Phase 2 fetch complete");
            status.text = sample.Length > 0
                ? "Example: \"[PlayFabCBSManager] Phase 2 fetch complete\" belongs to channel \"" + sample + "\"."
                : "Valid, but it does not match the usual \"[Tag] message\" form.";
            status.style.color = new UnityEngine.Color(0.6f, 0.6f, 0.6f);
        }
    }
}
