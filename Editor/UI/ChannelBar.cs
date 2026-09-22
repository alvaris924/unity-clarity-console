using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The chips under the toolbar, one per channel seen in the store, ordered by count. Clicking a chip
    /// narrows the list to that channel; clicking more adds them. With nothing selected every channel is
    /// shown, which is why no chip is highlighted by default. The chips wrap inside a scrolling area that
    /// sizes to a few rows on its own; dragging the bar's bottom edge gives it a height of its own, and a
    /// double-click on the edge hands the sizing back.
    /// </summary>
    internal sealed class ChannelBar : VisualElement
    {
        public const string ChipClass = "cc-chip";
        public const string ChipSelectedClass = "cc-chip-selected";
        public const string OverflowClass = "cc-chip-overflow";
        public const string RowClass = "cc-chip-row";
        public const string ScrollClass = "cc-channels-scroll";
        public const string HandleClass = "cc-channels-handle";

        /// <summary>Chips beyond this many are summarised by a trailing count.</summary>
        public const int MaxChips = 32;

        /// <summary>Without a dragged height the bar grows with its chips up to about three rows.</summary>
        public const int AutoMaxHeight = 64;

        public const int MinHeight = 22;
        public const int MaxHeight = 400;

        private readonly ScrollView _scroll;
        private readonly VisualElement _row;
        private readonly VisualElement _handle;
        private readonly List<string> _shown = new List<string>();
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
        private string _signature = string.Empty;
        private bool _visible = true;
        private int _heightOverride;
        private float _dragStartY;
        private float _dragStartHeight;
        private bool _dragging;

        public ChannelBar()
        {
            AddToClassList("cc-channels");

            _scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _scroll.AddToClassList(ScrollClass);
            _row = new VisualElement();
            _row.AddToClassList(RowClass);
            _scroll.Add(_row);
            Add(_scroll);

            _handle = new VisualElement { tooltip = "Drag to give the chips more or less room. Double-click to size to the chips again." };
            _handle.AddToClassList(HandleClass);
            _handle.RegisterCallback<PointerDownEvent>(OnHandleDown);
            _handle.RegisterCallback<PointerMoveEvent>(OnHandleMove);
            _handle.RegisterCallback<PointerUpEvent>(OnHandleUp);
            _handle.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            Add(_handle);

            ApplyHeight();
            ApplyDisplay();
        }

        /// <summary>Raised when a chip is clicked, with the channel and whether it is now selected.</summary>
        public event Action<string, bool> ChannelToggled;

        /// <summary>Raised when a drag ends or a double-click resets, with the new height (0 = size to the chips).</summary>
        public event Action<int> HeightChanged;

        /// <summary>Channels currently drawn, in display order. Test hook.</summary>
        public IReadOnlyList<string> ShownChannels => _shown;

        /// <summary>The wrapping row the chips live in. Test hook.</summary>
        public VisualElement ChipRow => _row;

        /// <summary>The bottom edge the user drags. Test hook.</summary>
        public VisualElement Handle => _handle;

        /// <summary>Whether the bar shows at all; off hides it even when there are channels.</summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value)
                {
                    return;
                }

                _visible = value;
                ApplyDisplay();
            }
        }

        /// <summary>The height the user dragged the bar to, in pixels; 0 lets it size to its chips.</summary>
        public int HeightOverride
        {
            get => _heightOverride;
            set
            {
                int clamped = value <= 0 ? 0 : Mathf.Clamp(value, MinHeight, MaxHeight);
                if (_heightOverride == clamped)
                {
                    return;
                }

                _heightOverride = clamped;
                ApplyHeight();
            }
        }

        /// <summary>
        /// Rebuilds the chips when the channels, their counts or the selection changed; otherwise does
        /// nothing, so this is cheap to call on a timer.
        /// </summary>
        public void Refresh(IEnumerable<KeyValuePair<string, int>> channelCounts, Func<string, bool> isSelected)
        {
            var ordered = new List<KeyValuePair<string, int>>(channelCounts);
            ordered.Sort(CompareByCountThenName);

            string signature = BuildSignature(ordered, isSelected);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            _row.Clear();
            _shown.Clear();
            _selected.Clear();

            int chips = Math.Min(ordered.Count, MaxChips);
            for (int i = 0; i < chips; i++)
            {
                KeyValuePair<string, int> channel = ordered[i];
                bool selected = isSelected(channel.Key);
                _shown.Add(channel.Key);
                if (selected)
                {
                    _selected.Add(channel.Key);
                }

                _row.Add(BuildChip(channel.Key, channel.Value, selected));
            }

            if (ordered.Count > chips)
            {
                var more = new Label("+" + (ordered.Count - chips) + " more");
                more.AddToClassList(ChipClass);
                more.AddToClassList(OverflowClass);
                more.tooltip = "Channels beyond the first " + MaxChips + " by count. Use search to reach them.";
                _row.Add(more);
            }

            ApplyDisplay();
        }

        /// <summary>
        /// Handles a chip click: asks for the channel to be selected unless it already is. The bar knows
        /// the selection from the last <see cref="Refresh"/>, so the caller is told the new state.
        /// </summary>
        internal void HandleChipClicked(string channel)
        {
            ChannelToggled?.Invoke(channel, !_selected.Contains(channel));
        }

        /// <summary>The drag arithmetic on its own, for tests: the height a drag from <paramref name="startHeight"/> by <paramref name="deltaY"/> lands on.</summary>
        internal static int DraggedHeight(float startHeight, float deltaY)
        {
            return Mathf.Clamp(Mathf.RoundToInt(startHeight + deltaY), MinHeight, MaxHeight);
        }

        /// <summary>
        /// A stable colour per channel name: the name's hash picks a hue, so the same manager keeps the
        /// same colour between sessions and between projects.
        /// </summary>
        internal static Color ColorFor(string channel)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < channel.Length; i++)
                {
                    hash = (hash ^ channel[i]) * 16777619;
                }

                float hue = (hash % 360) / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.95f);
            }
        }

        private void ApplyDisplay()
        {
            style.display = _visible && _shown.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyHeight()
        {
            if (_heightOverride > 0)
            {
                style.height = _heightOverride;
                style.maxHeight = StyleKeyword.None;
            }
            else
            {
                style.height = StyleKeyword.Auto;
                style.maxHeight = AutoMaxHeight;
            }
        }

        private void OnHandleDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            if (evt.clickCount >= 2)
            {
                HeightOverride = 0;
                HeightChanged?.Invoke(0);
                evt.StopPropagation();
                return;
            }

            _dragging = true;
            _dragStartY = evt.position.y;
            _dragStartHeight = float.IsNaN(resolvedStyle.height) ? AutoMaxHeight : resolvedStyle.height;
            _handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnHandleMove(PointerMoveEvent evt)
        {
            if (!_dragging || !_handle.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            HeightOverride = DraggedHeight(_dragStartHeight, evt.position.y - _dragStartY);
            evt.StopPropagation();
        }

        private void OnHandleUp(PointerUpEvent evt)
        {
            if (!_dragging)
            {
                return;
            }

            _handle.ReleasePointer(evt.pointerId);
            EndDrag();
            evt.StopPropagation();
        }

        private void EndDrag()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            HeightChanged?.Invoke(_heightOverride);
        }

        private static int CompareByCountThenName(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            int byCount = b.Value.CompareTo(a.Value);
            return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
        }

        private static string BuildSignature(List<KeyValuePair<string, int>> ordered, Func<string, bool> isSelected)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < ordered.Count && i <= MaxChips; i++)
            {
                builder.Append(ordered[i].Key).Append(':').Append(ordered[i].Value).Append(isSelected(ordered[i].Key) ? '+' : '-').Append('|');
            }

            builder.Append(ordered.Count);
            return builder.ToString();
        }

        private VisualElement BuildChip(string channel, int count, bool selected)
        {
            var chip = new Button { text = channel + "  " + count };
            chip.AddToClassList(ChipClass);
            chip.tooltip = selected
                ? "Showing only selected channels. Click to stop filtering by " + channel + "."
                : "Show only " + channel + ". Click more chips to add channels.";

            Color color = ColorFor(channel);
            chip.style.borderLeftColor = color;
            chip.style.borderRightColor = color;
            chip.style.borderTopColor = color;
            chip.style.borderBottomColor = color;
            chip.style.color = color;

            if (selected)
            {
                chip.AddToClassList(ChipSelectedClass);
                chip.style.backgroundColor = new Color(color.r, color.g, color.b, 0.25f);
            }

            chip.clicked += () => HandleChipClicked(channel);
            return chip;
        }
    }
}
