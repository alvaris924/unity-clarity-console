using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using ClarityConsole.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The small window behind "Tag as…": a tag name, how to match, and the pattern, filled in from the
    /// entry that was right-clicked so making a tag is one prompt. Adds a <see cref="TagRule"/> to the
    /// project settings, which re-tags every entry already captured.
    /// </summary>
    internal sealed class TagPromptWindow : EditorWindow
    {
        private static readonly Vector2 Size = new Vector2(420, 150);

        private LogEntry _entry;
        private Action<string> _notify;
        private TextField _tag;
        private EnumField _match;
        private TextField _pattern;

        /// <summary>Opens the prompt for an entry. <paramref name="notify"/> receives a one-line result for the status bar.</summary>
        public static void Open(LogEntry entry, Action<string> notify)
        {
            if (entry == null)
            {
                return;
            }

            var window = CreateInstance<TagPromptWindow>();
            window._entry = entry;
            window._notify = notify;
            window.titleContent = new GUIContent("Tag as");
            window.minSize = Size;
            window.maxSize = Size;
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            TagSuggestion suggestion = TagSuggestion.For(_entry);
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            _tag = new TextField("Tag") { value = suggestion.Tag };
            _tag.tooltip = "The channel name, as it will read on the chip.";
            root.Add(_tag);

            _match = new EnumField("Match", suggestion.Match);
            root.Add(_match);

            _pattern = new TextField("Pattern") { value = suggestion.Pattern };
            root.Add(_pattern);
            _match.RegisterValueChangedCallback(evt =>
            {
                // Switching the kind swaps in the suggestion that fits it, unless the user typed their own.
                string fitting = suggestion.PatternFor((TagMatch)evt.newValue);
                if (fitting.Length > 0 && (_pattern.value == suggestion.PatternFor((TagMatch)evt.previousValue) || _pattern.value.Length == 0))
                {
                    _pattern.SetValueWithoutNotify(fitting);
                }
            });

            var hint = new Label("An explicit [Tag] prefix always wins over a rule. Tags are project settings, shared with the team.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 6;
            hint.style.color = new Color(0.62f, 0.62f, 0.62f);
            root.Add(hint);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8;
            var cancel = new Button(Close) { text = "Cancel" };
            var ok = new Button(Apply) { text = "Create tag" };
            ok.style.marginLeft = 4;
            buttons.Add(cancel);
            buttons.Add(ok);
            root.Add(buttons);

            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Apply();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                }
            }, TrickleDown.TrickleDown);

            _tag.schedule.Execute(() => _tag.Q(TextField.textInputUssName)?.Focus());
        }

        private void Apply()
        {
            var match = (TagMatch)_match.value;
            var rule = new TagRule(_tag.value, match, _pattern.value);
            if (!rule.IsUsable)
            {
                _notify?.Invoke(rule.Error ?? "A tag needs a name and a pattern.");
                return;
            }

            bool added = ClarityConsoleSettings.instance.AddTagRule(rule.Tag, match, rule.Pattern);
            _notify?.Invoke(added
                ? "Tagged " + rule.Describe() + " as " + rule.Tag + ". Manage tags in Project Settings."
                : "That tag rule already exists.");
            Close();
        }
    }

    /// <summary>What "Tag as…" proposes for an entry: the caller when it has one, otherwise the message's start.</summary>
    internal readonly struct TagSuggestion
    {
        public const int MessageWords = 3;

        public TagSuggestion(string tag, TagMatch match, string pattern, string callerPattern, string messagePattern)
        {
            Tag = tag;
            Match = match;
            Pattern = pattern;
            CallerPattern = callerPattern;
            MessagePattern = messagePattern;
        }

        public string Tag { get; }

        public TagMatch Match { get; }

        public string Pattern { get; }

        public string CallerPattern { get; }

        public string MessagePattern { get; }

        /// <summary>The pattern that fits a match kind: the caller's name for Caller, the message start otherwise.</summary>
        public string PatternFor(TagMatch match)
        {
            return match == TagMatch.Caller ? CallerPattern : MessagePattern;
        }

        public static TagSuggestion For(LogEntry entry)
        {
            string callerTag = entry != null ? CallerFrame.TagFor(entry) : string.Empty;
            string messageStart = entry != null ? MessageStart(entry.Message) : string.Empty;

            if (callerTag.Length > 0)
            {
                return new TagSuggestion(callerTag, TagMatch.Caller, callerTag, callerTag, messageStart);
            }

            return new TagSuggestion(messageStart, TagMatch.Contains, messageStart, string.Empty, messageStart);
        }

        /// <summary>The first few words of a message, without a trailing number or punctuation, as a tag-sized handle.</summary>
        internal static string MessageStart(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            int newline = message.IndexOfAny(new[] { '\n', '\r' });
            string line = newline >= 0 ? message.Substring(0, newline) : message;
            string[] words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var kept = new List<string>();
            foreach (string word in words)
            {
                if (kept.Count == MessageWords)
                {
                    break;
                }

                string trimmed = word.TrimEnd(':', ',', ';', '.', '=');
                if (trimmed.Length == 0 || ContainsDigit(trimmed))
                {
                    // Numbers, ids and versions vary from message to message; the words before them are the handle.
                    break;
                }

                kept.Add(trimmed);
            }

            return string.Join(" ", kept);
        }

        private static bool ContainsDigit(string word)
        {
            foreach (char c in word)
            {
                if (char.IsDigit(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
