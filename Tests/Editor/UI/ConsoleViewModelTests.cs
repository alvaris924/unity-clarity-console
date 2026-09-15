using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ConsoleViewModelTests
    {
        [Test]
        public void Visible_FollowsAppendsIncrementally()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store);
            var changes = new List<ViewChange>();
            model.Changed += changes.Add;

            store.Append(Entry(LogSeverity.Log, "one"));
            store.Append(Entry(LogSeverity.Warning, "two"));

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "one", "two" }));
            Assert.That(changes, Is.EqualTo(new[] { ViewChange.Appended, ViewChange.Appended }));
        }

        [Test]
        public void SeverityFilter_HidesLogsButKeepsMarkers()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store);
            store.Append(LogEntry.Marker("Domain loaded", DateTime.UtcNow, 0));
            store.Append(Entry(LogSeverity.Log, "chatter"));
            store.Append(Entry(LogSeverity.Error, "broken"));

            model.SetSeverityVisible(LogSeverity.Log, false);

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "Domain loaded", "broken" }));
            Assert.That(model.IsSeverityVisible(LogSeverity.Log), Is.False);
        }

        [Test]
        public void Search_IsCaseInsensitiveSubstring_AndRebuilds()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store);
            var changes = new List<ViewChange>();
            store.Append(Entry(LogSeverity.Log, "Request TIMEOUT after 8000ms"));
            store.Append(Entry(LogSeverity.Log, "all good"));
            model.Changed += changes.Add;

            model.Search = "timeout";

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "Request TIMEOUT after 8000ms" }));
            Assert.That(changes, Is.EqualTo(new[] { ViewChange.Rebuilt }));

            model.Search = null;
            Assert.That(model.Visible.Count, Is.EqualTo(2));
        }

        [Test]
        public void Collapse_GroupsIdenticalSeverityAndMessage()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store) { Collapse = true };

            store.Append(Entry(LogSeverity.Log, "tick"));
            store.Append(Entry(LogSeverity.Log, "tick"));
            store.Append(Entry(LogSeverity.Warning, "tick"));
            store.Append(Entry(LogSeverity.Log, "tick"));

            Assert.That(model.Visible.Count, Is.EqualTo(2));
            Assert.That(model.CountAt(0), Is.EqualTo(3));
            Assert.That(model.CountAt(1), Is.EqualTo(1));

            model.Collapse = false;
            Assert.That(model.Visible.Count, Is.EqualTo(4));
        }

        [Test]
        public void Clear_EmptiesVisible()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "one"));

            store.Clear();

            Assert.That(model.Visible, Is.Empty);
        }

        [Test]
        public void Eviction_IsFoldedInLazilyByFlush()
        {
            var store = new LogStore(capacity: 2);
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "one"));
            store.Append(Entry(LogSeverity.Log, "two"));

            store.Append(Entry(LogSeverity.Log, "three"));

            Assert.That(model.NeedsRebuild, Is.True);
            Assert.That(model.Visible.Count, Is.EqualTo(3), "evicted row still listed until flush");

            model.Flush();

            Assert.That(model.NeedsRebuild, Is.False);
            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "two", "three" }));
        }

        [Test]
        public void Dispose_StopsFollowingTheStore()
        {
            var store = new LogStore(capacity: 16);
            var model = new ConsoleViewModel(store);

            model.Dispose();
            store.Append(Entry(LogSeverity.Log, "late"));

            Assert.That(model.Visible, Is.Empty);
        }

        [Test]
        public void ChannelSelection_NarrowsToSelectedChannels_AndKeepsMarkers()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[Net] one"));
            store.Append(Entry(LogSeverity.Log, "[UI] two"));
            store.Append(Entry(LogSeverity.Log, "untagged"));
            store.Append(LogEntry.Marker("Domain reloaded", DateTime.UtcNow, 0));

            model.SetChannelSelected("Net", true);

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] one", "Domain reloaded" }));
            Assert.That(model.IsChannelSelected("Net"), Is.True);

            model.SetChannelSelected("UI", true);
            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] one", "[UI] two", "Domain reloaded" }));

            model.ClearChannelSelection();
            Assert.That(model.Visible.Count, Is.EqualTo(4));
            Assert.That(model.SelectedChannels, Is.Empty);
        }

        [Test]
        public void ChannelSelection_CombinesWithSearchAndSeverity()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[Net] timeout after 8000ms"));
            store.Append(Entry(LogSeverity.Warning, "[Net] timeout after 9000ms"));
            store.Append(Entry(LogSeverity.Log, "[Net] connected"));
            store.Append(Entry(LogSeverity.Log, "[UI] timeout ignored"));

            model.SetChannelSelected("Net", true);
            model.Search = "timeout";
            model.SetSeverityVisible(LogSeverity.Warning, false);

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] timeout after 8000ms" }));
        }

        [Test]
        public void SetChannelSelected_IgnoresEmptyNames_AndRepeats()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[Net] one"));
            int rebuilds = 0;
            model.Changed += change => { if (change == ViewChange.Rebuilt) rebuilds++; };

            model.SetChannelSelected(null, true);
            model.SetChannelSelected(string.Empty, true);
            model.SetChannelSelected("Net", true);
            model.SetChannelSelected("Net", true);

            Assert.That(rebuilds, Is.EqualTo(1));
            Assert.That(model.SelectedChannels, Is.EqualTo(new[] { "Net" }));
        }

        [Test]
        public void ReassignChannels_DropsSelectionsThatNoLongerExist_AndRebuilds()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[Net] one"));
            store.Append(Entry(LogSeverity.Log, "UI>> two"));
            model.SetChannelSelected("Net", true);

            store.ReassignChannels(new ChannelExtractor(@"^(\w+)>>"));

            Assert.That(model.SelectedChannels, Is.Empty);
            Assert.That(model.Visible.Count, Is.EqualTo(2));
        }

        private static LogEntry Entry(LogSeverity severity, string message)
        {
            return new LogEntry(LogEntryKind.Log, severity, message, string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
