using System.Collections.Generic;
using System.Linq;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ChannelBarTests
    {
        [Test]
        public void Refresh_OrdersByCountThenName_AndMarksTheSelection()
        {
            var bar = new ChannelBar();

            bar.Refresh(Counts(("UI", 3), ("Net", 9), ("Audio", 3)), channel => channel == "Net");

            Assert.That(bar.ShownChannels, Is.EqualTo(new[] { "Net", "Audio", "UI" }));
            Button[] chips = bar.Query<Button>(className: ChannelBar.ChipClass).ToList().ToArray();
            Assert.That(chips[0].text, Is.EqualTo("Net  9"));
            Assert.That(chips[0].ClassListContains(ChannelBar.ChipSelectedClass), Is.True);
            Assert.That(chips[1].ClassListContains(ChannelBar.ChipSelectedClass), Is.False);
        }

        [Test]
        public void ChipClick_RaisesToggle_WithTheOppositeSelection()
        {
            var bar = new ChannelBar();
            var raised = new List<KeyValuePair<string, bool>>();
            bar.ChannelToggled += (channel, selected) => raised.Add(new KeyValuePair<string, bool>(channel, selected));
            bar.Refresh(Counts(("Net", 2), ("UI", 1)), channel => channel == "UI");

            bar.HandleChipClicked("Net");
            bar.HandleChipClicked("UI");

            Assert.That(raised, Is.EqualTo(new[]
            {
                new KeyValuePair<string, bool>("Net", true),
                new KeyValuePair<string, bool>("UI", false),
            }));
        }

        [Test]
        public void Refresh_BeyondTheChipLimit_SummarisesTheRest()
        {
            var bar = new ChannelBar();
            var counts = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < ChannelBar.MaxChips + 5; i++)
            {
                counts.Add(new KeyValuePair<string, int>("Channel" + i.ToString("D2"), 100 - i));
            }

            bar.Refresh(counts, _ => false);

            Assert.That(bar.ShownChannels.Count, Is.EqualTo(ChannelBar.MaxChips));
            Label overflow = bar.Query<Label>(className: ChannelBar.OverflowClass).First();
            Assert.That(overflow.text, Is.EqualTo("+5 more"));
        }

        [Test]
        public void Refresh_WithNoChannels_HidesTheBar()
        {
            var bar = new ChannelBar();

            bar.Refresh(Counts(("Net", 1)), _ => false);
            Assert.That(bar.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            bar.Refresh(Counts(), _ => false);

            Assert.That(bar.childCount, Is.EqualTo(0));
            Assert.That(bar.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Refresh_WithoutChanges_KeepsTheSameElements()
        {
            var bar = new ChannelBar();
            bar.Refresh(Counts(("Net", 1)), _ => false);
            VisualElement first = bar[0];

            bar.Refresh(Counts(("Net", 1)), _ => false);
            Assert.That(bar[0], Is.SameAs(first), "an unchanged refresh must not rebuild");

            bar.Refresh(Counts(("Net", 2)), _ => false);
            Assert.That(bar[0], Is.Not.SameAs(first), "a changed count rebuilds");
        }

        [Test]
        public void ColorFor_IsStablePerName_AndDiffersBetweenNames()
        {
            Assert.That(ChannelBar.ColorFor("PlayFabCBSManager"), Is.EqualTo(ChannelBar.ColorFor("PlayFabCBSManager")));
            Assert.That(ChannelBar.ColorFor("PlayFabCBSManager"), Is.Not.EqualTo(ChannelBar.ColorFor("LevelController")));
        }

        private static List<KeyValuePair<string, int>> Counts(params (string Channel, int Count)[] pairs)
        {
            return pairs.Select(p => new KeyValuePair<string, int>(p.Channel, p.Count)).ToList();
        }
    }
}
