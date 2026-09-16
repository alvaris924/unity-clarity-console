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

        private static void Build(VisualElement root)
        {
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            var title = new Label("Channels");
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.marginBottom = 4;
            root.Add(title);

            var help = new Label(
                "Messages that start with a tag are grouped into channels you can filter by. " +
                "The first capture group names the channel; only the first " + ChannelExtractor.MaxPrefixLength +
                " characters of a message are examined.");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginBottom = 6;
            root.Add(help);

            var status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = 4;

            var field = new TextField("Channel pattern") { value = ClarityConsoleSettings.instance.ChannelPattern };
            field.RegisterValueChangedCallback(evt =>
            {
                ClarityConsoleSettings.instance.ChannelPattern = evt.newValue;
                UpdateStatus(status, evt.newValue);
            });
            root.Add(field);
            root.Add(status);
            UpdateStatus(status, field.value);

            var watchTitle = new Label("Watch rows");
            watchTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            watchTitle.style.marginTop = 12;
            watchTitle.style.marginBottom = 4;
            root.Add(watchTitle);

            var watchHelp = new Label(
                "Messages that start with a watch key, " + WatchExample + " by default, replace each other " +
                "in the window instead of piling up, so a value you log every frame reads as one line that " +
                "changes. Leave the pattern empty to turn this off.");
            watchHelp.style.whiteSpace = WhiteSpace.Normal;
            watchHelp.style.marginBottom = 4;
            root.Add(watchHelp);

            var watch = new TextField("Watch pattern") { value = ClarityConsoleSettings.instance.WatchPattern };
            watch.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.WatchPattern = evt.newValue);
            root.Add(watch);

            var frames = new Label("Stack frames");
            frames.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            frames.style.marginTop = 12;
            frames.style.marginBottom = 4;
            root.Add(frames);

            var radius = new SliderInt("Preview lines", ClarityConsoleSettings.MinSourcePreviewRadius, ClarityConsoleSettings.MaxSourcePreviewRadius)
            {
                value = ClarityConsoleSettings.instance.SourcePreviewRadius,
                showInputField = true,
            };
            radius.tooltip = "Lines of source shown on each side of the line a stack frame points at. Zero shows only that line.";
            radius.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.SourcePreviewRadius = evt.newValue);
            root.Add(radius);

            var hoverRadius = new SliderInt("Hover card lines", ClarityConsoleSettings.MinSourcePreviewRadius, ClarityConsoleSettings.MaxSourceHoverRadius)
            {
                value = ClarityConsoleSettings.instance.SourceHoverRadius,
                showInputField = true,
            };
            hoverRadius.tooltip = "Lines of source shown on each side of the frame's line in the card that appears while the pointer rests on a frame or its source block.";
            hoverRadius.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.SourceHoverRadius = evt.newValue);
            root.Add(hoverRadius);

            var hideEngine = new Toggle("Fold engine frames")
            {
                value = ClarityConsoleSettings.instance.HideEngineFrames,
            };
            hideEngine.tooltip = "Fold frames from the engine, the Editor, the runtime and this package away from the stack, so your own code is what you see first.";
            hideEngine.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.HideEngineFrames = evt.newValue);
            root.Add(hideEngine);

            var prefixes = new TextField("Also fold types starting with") { multiline = true, value = ClarityConsoleSettings.instance.HiddenFramePrefixes };
            prefixes.tooltip = "One type-name prefix per line, for example Cysharp.Threading.Tasks. Lines starting with # are comments.";
            prefixes.style.minHeight = 54;
            prefixes.RegisterValueChangedCallback(evt => ClarityConsoleSettings.instance.HiddenFramePrefixes = evt.newValue);
            root.Add(prefixes);

            var ignoreTitle = new Label("Ignored messages");
            ignoreTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            ignoreTitle.style.marginTop = 12;
            ignoreTitle.style.marginBottom = 4;
            root.Add(ignoreTitle);

            var ignoreHelp = new Label(
                "Entries matching these rules are hidden from the console window. They are still captured " +
                "and journaled, so removing a rule brings them back. Add rules by right-clicking a row in the console.");
            ignoreHelp.style.whiteSpace = WhiteSpace.Normal;
            ignoreHelp.style.marginBottom = 4;
            root.Add(ignoreHelp);

            var rules = new VisualElement();
            root.Add(rules);
            RebuildRules(rules);

            var reset = new Button(() =>
            {
                ClarityConsoleSettings.instance.ResetToDefaults();
                field.SetValueWithoutNotify(ClarityConsoleSettings.instance.ChannelPattern);
                watch.SetValueWithoutNotify(ClarityConsoleSettings.instance.WatchPattern);
                radius.SetValueWithoutNotify(ClarityConsoleSettings.instance.SourcePreviewRadius);
                hoverRadius.SetValueWithoutNotify(ClarityConsoleSettings.instance.SourceHoverRadius);
                hideEngine.SetValueWithoutNotify(ClarityConsoleSettings.instance.HideEngineFrames);
                prefixes.SetValueWithoutNotify(ClarityConsoleSettings.instance.HiddenFramePrefixes);
                UpdateStatus(status, field.value);
                RebuildRules(rules);
            })
            {
                text = "Reset to default",
            };
            reset.style.marginTop = 8;
            reset.style.width = 140;
            root.Add(reset);
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
