using ClarityConsole.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// A floating card with a longer stretch of source around a frame's line, shown while the pointer
    /// rests on a frame or its source block. It floats in a host element (the window root) so it can
    /// overlap the pane, and places itself below and to the right of the pointer, flipping to the other
    /// side when it would leave the host.
    /// </summary>
    internal sealed class SourceHoverCard : VisualElement
    {
        public const string CardClass = "cc-hover";
        public const float Offset = 14f;

        private readonly Label _header;
        private readonly VisualElement _lines;
        private VisualElement _host;
        private Vector2 _anchor;

        public SourceHoverCard()
        {
            AddToClassList(CardClass);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.display = DisplayStyle.None;

            _header = new Label();
            _header.AddToClassList(SourcePreview.HeaderClass);
            Add(_header);

            _lines = new VisualElement();
            _lines.AddToClassList("cc-src-lines");
            Add(_lines);

            RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public bool IsShown => style.display == DisplayStyle.Flex;

        /// <summary>Header text, for tests.</summary>
        public string HeaderText => _header.text;

        /// <summary>Source lines drawn, for tests.</summary>
        public int LineCount => _lines.childCount;

        /// <summary>
        /// Shows <paramref name="snippet"/> inside <paramref name="host"/> near <paramref name="anchor"/>,
        /// given in the host's coordinates. A null snippet hides the card.
        /// </summary>
        public void Show(VisualElement host, SourceSnippet snippet, string displayPath, Vector2 anchor)
        {
            if (snippet == null || host == null)
            {
                Hide();
                return;
            }

            if (parent != host)
            {
                RemoveFromHierarchy();
                host.Add(this);
            }

            _host = host;
            _anchor = anchor;
            _header.text = displayPath + ":" + snippet.HighlightLine;
            SourcePreview.FillLines(_lines, snippet);

            // Start at the pointer; the geometry callback moves the card once its size is known.
            style.left = anchor.x + Offset;
            style.top = anchor.y + Offset;
            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }

        private void Place()
        {
            if (_host == null || !IsShown)
            {
                return;
            }

            Vector2 size = new Vector2(resolvedStyle.width, resolvedStyle.height);
            Vector2 hostSize = new Vector2(_host.resolvedStyle.width, _host.resolvedStyle.height);
            if (float.IsNaN(size.x) || float.IsNaN(hostSize.x))
            {
                return;
            }

            Vector2 position = Fit(_anchor, size, hostSize, Offset);
            style.left = position.x;
            style.top = position.y;
        }

        /// <summary>
        /// Where a card of <paramref name="size"/> goes for a pointer at <paramref name="anchor"/>: below
        /// and to the right by <paramref name="offset"/>, flipped above or to the left when that would
        /// leave the host, and clamped to the host when it fits neither way.
        /// </summary>
        internal static Vector2 Fit(Vector2 anchor, Vector2 size, Vector2 hostSize, float offset)
        {
            return new Vector2(
                FitAxis(anchor.x, size.x, hostSize.x, offset),
                FitAxis(anchor.y, size.y, hostSize.y, offset));
        }

        private static float FitAxis(float anchor, float size, float hostSize, float offset)
        {
            float after = anchor + offset;
            if (after + size <= hostSize)
            {
                return after;
            }

            float before = anchor - offset - size;
            if (before >= 0)
            {
                return before;
            }

            return Mathf.Max(0, hostSize - size);
        }
    }
}
