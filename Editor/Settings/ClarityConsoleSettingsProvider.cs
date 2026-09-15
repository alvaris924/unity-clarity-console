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

            var reset = new Button(() =>
            {
                ClarityConsoleSettings.instance.ResetToDefaults();
                field.SetValueWithoutNotify(ClarityConsoleSettings.instance.ChannelPattern);
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
