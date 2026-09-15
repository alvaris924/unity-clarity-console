using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEditor;
using UnityEngine.UIElements;

namespace ClarityConsole.Settings
{
    /// <summary>The Project Settings page for Clarity Console.</summary>
    internal static class ClarityConsoleSettingsProvider
    {
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

            var reset = new Button(() =>
            {
                ClarityConsoleSettings.instance.ResetToDefaults();
                field.SetValueWithoutNotify(ClarityConsoleSettings.instance.ChannelPattern);
                radius.SetValueWithoutNotify(ClarityConsoleSettings.instance.SourcePreviewRadius);
                hideEngine.SetValueWithoutNotify(ClarityConsoleSettings.instance.HideEngineFrames);
                prefixes.SetValueWithoutNotify(ClarityConsoleSettings.instance.HiddenFramePrefixes);
                UpdateStatus(status, field.value);
            })
            {
                text = "Reset to default",
            };
            reset.style.marginTop = 8;
            reset.style.width = 140;
            root.Add(reset);
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
