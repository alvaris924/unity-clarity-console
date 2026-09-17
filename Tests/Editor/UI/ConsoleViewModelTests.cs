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
        public void DomainReloadMarkers_AreHiddenByDefault_AndShownOnRequest_WhileOtherMarkersAlwaysShow()
        {
            var store = new LogStore(16);
            using (var vm = new ConsoleViewModel(store))
            {
                store.Append(LogEntry.Marker(LogEntry.EditorStartedMarker, DateTime.UtcNow, 0));
                store.Append(LogEntry.Marker(LogEntry.DomainReloadMarker, DateTime.UtcNow, 0));
                store.Append(LogEntry.Marker("Entered Play mode, session 3", DateTime.UtcNow, 0));
                vm.Flush();

                Assert.That(vm.Visible.Select(e => e.Message), Is.EqualTo(new[] { LogEntry.EditorStartedMarker, "Entered Play mode, session 3" }));

                vm.ShowDomainReloads = true;
                Assert.That(vm.Visible.Count, Is.EqualTo(3));
                Assert.That(vm.Visible[1].IsDomainReloadMarker, Is.True);

                vm.ShowDomainReloads = false;
                Assert.That(vm.Visible.Count, Is.EqualTo(2));
            }
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
        public void Search_UsesTheQueryLanguage_AndReportsErrors()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Error, "[Net] request timeout 8000ms"));
            store.Append(Entry(LogSeverity.Log, "[Net] request timeout, retry scheduled"));
            store.Append(Entry(LogSeverity.Log, "[UI] all good"));

            model.Search = "timeout -\"retry scheduled\"";
            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] request timeout 8000ms" }));
            Assert.That(model.QueryError, Is.Null);

            model.Search = "sev:error OR tag:UI";
            Assert.That(model.Visible.Count, Is.EqualTo(2));

            model.Search = "/unclosed";
            Assert.That(model.QueryError, Is.Not.Null);
            Assert.That(model.Visible, Is.Empty, "a malformed query matches nothing");

            model.Search = string.Empty;
            Assert.That(model.QueryError, Is.Null);
            Assert.That(model.Visible.Count, Is.EqualTo(3));
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
            store.Append(LogEntry.Marker("Entered Play mode, session 1", DateTime.UtcNow, 0));

            model.SetChannelSelected("Net", true);

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] one", "Entered Play mode, session 1" }));
            Assert.That(model.IsChannelSelected("Net"), Is.True);

            model.SetChannelSelected("UI", true);
            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] one", "[UI] two", "Entered Play mode, session 1" }));

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

        [Test]
        public void IgnoreList_HidesMatchingEntries_AndCountsThem()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[Noisy] tick 1"));
            store.Append(Entry(LogSeverity.Log, "[Noisy] tick 2"));
            store.Append(Entry(LogSeverity.Error, "[Net] real problem"));

            model.IgnoreList = new IgnoreList(new[] { new IgnoreRule(IgnoreMatch.Channel, "Noisy") });

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[] { "[Net] real problem" }));
            Assert.That(model.IgnoredCount, Is.EqualTo(2));
        }

        [Test]
        public void IgnoreList_CountsIndependentlyOfSearch_AndIsUndoneByClearingIt()
        {
            var store = new LogStore(capacity: 16) { ChannelExtractor = new ChannelExtractor() };
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "tick"));
            store.Append(Entry(LogSeverity.Log, "keep me"));
            model.IgnoreList = new IgnoreList(new[] { new IgnoreRule(IgnoreMatch.Contains, "tick") });

            model.Search = "nothing matches this";
            Assert.That(model.Visible, Is.Empty);
            Assert.That(model.IgnoredCount, Is.EqualTo(1), "ignored entries are counted before the query runs");

            model.Search = string.Empty;
            model.IgnoreList = null;
            Assert.That(model.Visible.Count, Is.EqualTo(2));
            Assert.That(model.IgnoredCount, Is.EqualTo(0));
        }

        [Test]
        public void IgnoreList_NeverHidesMarkers()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store);
            store.Append(LogEntry.Marker("Entered Play mode, session 1", DateTime.UtcNow, 0));

            model.IgnoreList = new IgnoreList(new[] { new IgnoreRule(IgnoreMatch.Contains, "Domain") });

            Assert.That(model.Visible.Count, Is.EqualTo(1));
            Assert.That(model.IgnoredCount, Is.EqualTo(0));
        }

        [Test]
        public void WatchEntries_ShareOneRow_ThatKeepsItsPlaceAndShowsTheLatest()
        {
            var store = Store();
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 100"));
            store.Append(Entry(LogSeverity.Log, "before"));
            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 80"));
            store.Append(Entry(LogSeverity.Log, "[watch:Ammo] 30"));
            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 55"));

            Assert.That(model.Visible.Select(e => e.Message), Is.EqualTo(new[]
            {
                "[watch:PlayerHP] 55",
                "before",
                "[watch:Ammo] 30",
            }));
            Assert.That(model.CountAt(0), Is.EqualTo(3), "the row counts its updates");
            Assert.That(model.CountAt(2), Is.EqualTo(1));
        }

        [Test]
        public void WatchRows_RespectFiltersAndSurviveRebuilds()
        {
            var store = Store();
            using var model = new ConsoleViewModel(store);
            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 100"));
            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 42"));

            model.Search = "42";
            Assert.That(model.Visible.Single().Message, Is.EqualTo("[watch:PlayerHP] 42"));

            model.Search = "100";
            Assert.That(model.Visible.Single().Message, Is.EqualTo("[watch:PlayerHP] 100"), "older values are still in the store");

            model.Search = string.Empty;
            Assert.That(model.Visible.Count, Is.EqualTo(1));
            Assert.That(model.CountAt(0), Is.EqualTo(2));
        }

        [Test]
        public void WithoutAWatchExtractor_EveryEntryGetsItsOwnRow()
        {
            var store = new LogStore(capacity: 16);
            using var model = new ConsoleViewModel(store);

            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 100"));
            store.Append(Entry(LogSeverity.Log, "[watch:PlayerHP] 80"));

            Assert.That(model.Visible.Count, Is.EqualTo(2));
        }

        private static LogStore Store()
        {
            return new LogStore(capacity: 16)
            {
                ChannelExtractor = new ChannelExtractor(),
                WatchExtractor = new WatchExtractor(),
            };
        }

        private static LogEntry Entry(LogSeverity severity, string message)
        {
            return new LogEntry(LogEntryKind.Log, severity, message, string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
