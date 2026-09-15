using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The row of channel chips under the toolbar. One chip per channel seen in the store, ordered by
    /// count. Clicking a chip narrows the list to that channel; clicking more adds them. With nothing
    /// selected every channel is shown, which is why no chip is highlighted by default.
    /// </summary>
    internal sealed class ChannelBar : VisualElement
    {
        public const string ChipClass = "cc-chip";
        public const string ChipSelectedClass = "cc-chip-selected";
        public const string OverflowClass = "cc-chip-overflow";

        /// <summary>Chips beyond this many are summarised by a trailing count.</summary>
        public const int MaxChips = 16;

        private readonly List<string> _shown = new List<string>();
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
        private string _signature = string.Empty;

        public ChannelBar()
        {
            AddToClassList("cc-channels");
        }

        /// <summary>Raised when a chip is clicked, with the channel and whether it is now selected.</summary>
        public event Action<string, bool> ChannelToggled;

        /// <summary>Channels currently drawn, in display order. Test hook.</summary>
        public IReadOnlyList<string> ShownChannels => _shown;

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
            Clear();
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

                Add(BuildChip(channel.Key, channel.Value, selected));
            }

            if (ordered.Count > chips)
            {
                var more = new Label("+" + (ordered.Count - chips) + " more");
                more.AddToClassList(ChipClass);
                more.AddToClassList(OverflowClass);
                more.tooltip = "Channels beyond the first " + MaxChips + " by count. Use search to reach them.";
                Add(more);
            }

            style.display = ordered.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Handles a chip click: asks for the channel to be selected unless it already is. The bar knows
        /// the selection from the last <see cref="Refresh"/>, so the caller is told the new state.
        /// </summary>
        internal void HandleChipClicked(string channel)
        {
            ChannelToggled?.Invoke(channel, !_selected.Contains(channel));
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
