using System;
using ClarityConsole.Core;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// Shows a few source lines around the line a stack frame points at, with that line highlighted.
    /// Double-clicking opens the file in the code editor.
    /// </summary>
    internal sealed class SourcePreview : VisualElement
    {
        public const string LineClass = "cc-src-line";
        public const string HighlightClass = "cc-src-hit";
        public const string HeaderClass = "cc-src-header";

        private readonly Label _header;
        private readonly VisualElement _lines;

        public SourcePreview()
        {
            AddToClassList("cc-src");

            _header = new Label();
            _header.AddToClassList(HeaderClass);
            Add(_header);

            _lines = new VisualElement();
            _lines.AddToClassList("cc-src-lines");
            Add(_lines);

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount >= 2)
                {
                    Activated?.Invoke();
                }
            });
        }

        /// <summary>Raised when the preview is double-clicked.</summary>
        public event Action Activated;

        /// <summary>Line numbers currently drawn, for tests.</summary>
        public int LineCount => _lines.childCount;

        public void Show(SourceSnippet snippet, string displayPath)
        {
            _lines.Clear();

            if (snippet == null)
            {
                _header.text = string.Empty;
                tooltip = null;
                style.display = DisplayStyle.None;
                return;
            }

            style.display = DisplayStyle.Flex;
            _header.text = displayPath + ":" + snippet.HighlightLine;
            tooltip = "Double-click to open " + displayPath + " at line " + snippet.HighlightLine + ".";

            int width = (snippet.FirstLine + snippet.Lines.Count).ToString().Length;
            for (int i = 0; i < snippet.Lines.Count; i++)
            {
                int number = snippet.FirstLine + i;
                var line = new Label(number.ToString().PadLeft(width) + "  " + snippet.Lines[i].Replace("\t", "    "));
                line.AddToClassList(LineClass);
                if (i == snippet.HighlightIndex)
                {
                    line.AddToClassList(HighlightClass);
                }

                _lines.Add(line);
            }
        }

        public void Hide()
        {
            Show(null, null);
        }
    }
}
