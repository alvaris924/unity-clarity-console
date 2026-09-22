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

            Assert.That(bar.ChipRow.childCount, Is.EqualTo(0));
            Assert.That(bar.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Visible_Off_HidesTheBar_EvenWithChannels()
        {
            var bar = new ChannelBar();
            bar.Refresh(Counts(("Net", 1)), _ => false);

            bar.Visible = false;
            Assert.That(bar.style.display.value, Is.EqualTo(DisplayStyle.None));

            bar.Refresh(Counts(("Net", 2)), _ => false);
            Assert.That(bar.style.display.value, Is.EqualTo(DisplayStyle.None), "a refresh does not bring a hidden bar back");

            bar.Visible = true;
            Assert.That(bar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void Height_SizesToTheChipsUntilDragged_AndClamps()
        {
            var bar = new ChannelBar();

            Assert.That(bar.HeightOverride, Is.EqualTo(0));
            Assert.That(bar.style.height.keyword, Is.EqualTo(StyleKeyword.Auto));
            Assert.That(bar.style.maxHeight.value.value, Is.EqualTo(ChannelBar.AutoMaxHeight));

            bar.HeightOverride = 120;
            Assert.That(bar.style.height.value.value, Is.EqualTo(120f));
            Assert.That(bar.style.maxHeight.keyword, Is.EqualTo(StyleKeyword.None));

            bar.HeightOverride = 5;
            Assert.That(bar.HeightOverride, Is.EqualTo(ChannelBar.MinHeight), "too small clamps to one row");
            bar.HeightOverride = 9999;
            Assert.That(bar.HeightOverride, Is.EqualTo(ChannelBar.MaxHeight));

            bar.HeightOverride = 0;
            Assert.That(bar.style.height.keyword, Is.EqualTo(StyleKeyword.Auto), "zero hands the sizing back");
            Assert.That(bar.Handle, Is.Not.Null);
            Assert.That(bar.Handle.ClassListContains(ChannelBar.HandleClass), Is.True);
        }

        [Test]
        public void DraggedHeight_FollowsThePointer_WithinTheLimits()
        {
            Assert.That(ChannelBar.DraggedHeight(60, 40), Is.EqualTo(100));
            Assert.That(ChannelBar.DraggedHeight(60, -100), Is.EqualTo(ChannelBar.MinHeight));
            Assert.That(ChannelBar.DraggedHeight(60, 10000), Is.EqualTo(ChannelBar.MaxHeight));
            Assert.That(ChannelBar.DraggedHeight(60.4f, 0), Is.EqualTo(60));
        }

        [Test]
        public void Refresh_WithoutChanges_KeepsTheSameElements()
        {
            var bar = new ChannelBar();
            bar.Refresh(Counts(("Net", 1)), _ => false);
            VisualElement first = bar.ChipRow[0];

            bar.Refresh(Counts(("Net", 1)), _ => false);
            Assert.That(bar.ChipRow[0], Is.SameAs(first), "an unchanged refresh must not rebuild");

            bar.Refresh(Counts(("Net", 2)), _ => false);
            Assert.That(bar.ChipRow[0], Is.Not.SameAs(first), "a changed count rebuilds");
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
