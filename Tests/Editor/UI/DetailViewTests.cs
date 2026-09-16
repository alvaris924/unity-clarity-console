using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class DetailViewTests
    {
        [Test]
        public void Show_BuildsOneRowPerFrame_AndLinksOnlyLocatedFrames()
        {
            var view = new DetailView();
            LogEntry entry = Entry(
                "[Test] boom",
                "UnityEngine.Debug:Log (object)\n" +
                "Cysharp.Threading.Tasks.UniTask:Run () (at Library/PackageCache/com.cysharp.unitask@2.5.0/Runtime/UniTask.cs:30)\n" +
                "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)\n");

            view.Show(entry);

            Label[] rows = view.Query<Label>(className: DetailView.FrameClass).ToList().ToArray();
            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows[0].ClassListContains(DetailView.FrameLinkClass), Is.False);
            Assert.That(rows[1].ClassListContains(DetailView.FrameLinkClass), Is.True);
            Assert.That(rows[2].ClassListContains(DetailView.FrameLinkClass), Is.True);
            Assert.That(rows[2].ClassListContains(DetailView.FrameEntryClass), Is.True, "entry frame is emphasized");
            Assert.That(rows[1].ClassListContains(DetailView.FrameEntryClass), Is.False);
            Assert.That(rows[2].text, Is.EqualTo("Game.Foo:Bar ()    Assets/Game/Foo.cs:20"));
            Assert.That(view.Q<TextField>().value, Is.EqualTo("[Test] boom"));
        }

        [Test]
        public void Show_Null_ClearsMessageAndRows()
        {
            var view = new DetailView();
            view.Show(Entry("first", "A:B () (at Assets/A.cs:1)"));

            view.Show(null);

            Assert.That(view.FrameRowCount, Is.EqualTo(0));
            Assert.That(view.Q<TextField>().value, Is.Empty);
        }

        [Test]
        public void Show_Null_HidesThePreview_AndForgetsTheEntry()
        {
            var view = new DetailView();
            view.SnippetProvider = _ => new SourceSnippet("Assets/Game/Foo.cs", 19, new[] { "a", "b", "c" }, 1);
            view.Show(Entry("boom", Trace("Game.Foo:Bar () (at Assets/Game/Foo.cs:20)")));
            Assert.That(view.IsPreviewVisible, Is.True, "precondition: a located frame previews its source");
            Assert.That(view.Entry, Is.Not.Null);

            view.Show(null);

            Assert.That(view.Entry, Is.Null);
            Assert.That(view.SelectedFrame, Is.Null);
            Assert.That(view.IsPreviewVisible, Is.False, "the highlighted source line must go with the entry");
            Assert.That(view.Query<Label>(className: SourcePreview.HighlightClass).ToList(), Is.Empty);
        }

        [Test]
        public void Show_Marker_HasNoRows()
        {
            var view = new DetailView();

            view.Show(LogEntry.Marker("Entered Play mode", DateTime.UtcNow, 0));

            Assert.That(view.FrameRowCount, Is.EqualTo(0));
            Assert.That(view.Q<TextField>().value, Is.EqualTo("Entered Play mode"));
        }

        [Test]
        public void Show_SelectsTheEntryFrame_AndPreviewsIt()
        {
            var view = new DetailView();
            var asked = new List<TraceFrame>();
            view.SnippetProvider = frame =>
            {
                asked.Add(frame);
                return new SourceSnippet("Assets/Game/Foo.cs", 19, new[] { "a", "b", "c" }, 1);
            };

            view.Show(Entry("boom", Trace("UnityEngine.Debug:Log (object)", "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)")));

            Assert.That(view.SelectedFrame, Is.Not.Null);
            Assert.That(view.SelectedFrame.Line, Is.EqualTo(20));
            Assert.That(asked.Count, Is.EqualTo(1));
            Assert.That(view.IsPreviewVisible, Is.True);
            Label row = view.Query<Label>(className: DetailView.FrameSelectedClass).First();
            Assert.That(row.text, Does.Contain("Foo.cs:20"));
        }

        [Test]
        public void SelectFrame_MovesThePreview_AndHidesItForFramesWithoutALocation()
        {
            var view = new DetailView();
            view.SnippetProvider = frame => new SourceSnippet(frame.FilePath, frame.Line, new[] { "x" }, 0);
            LogEntry entry = Entry("boom", Trace("Game.A:One () (at Assets/A.cs:1)", "Game.B:Two () (at Assets/B.cs:2)"));
            view.Show(entry);

            TraceFrame second = entry.Trace.Frames[1];
            view.SelectFrame(second);

            Assert.That(view.SelectedFrame, Is.SameAs(second));
            Assert.That(view.Query<Label>(className: DetailView.FrameSelectedClass).ToList().Count, Is.EqualTo(1));

            view.SelectFrame(null);
            Assert.That(view.IsPreviewVisible, Is.False);
        }

        [Test]
        public void Show_WithoutASnippetProvider_HidesThePreview()
        {
            var view = new DetailView();

            view.Show(Entry("boom", "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)"));

            Assert.That(view.SelectedFrame, Is.Not.Null);
            Assert.That(view.IsPreviewVisible, Is.False);
        }

        [Test]
        public void Show_WithAFrameFilter_FoldsInfrastructureRuns()
        {
            var view = new DetailView
            {
                FrameFilter = new FrameFilter(hideEngineFrames: true, new[] { "Game.Logging." }),
            };

            view.Show(Entry("boom", Trace(
                "UnityEngine.Debug:Log (object)",
                "Game.Logging.Log:Info () (at Assets/Game/Logging/Log.cs:10)",
                "Game.Boss:Hit () (at Assets/Game/Boss.cs:88)",
                "UnityEngine.MonoBehaviour:Update ()")));

            Assert.That(view.FrameRowCount, Is.EqualTo(3), "two folded runs and the user's frame");
            Assert.That(view.HiddenFrameCount, Is.EqualTo(3));
            Label folded = view.Query<Label>(className: DetailView.FrameHiddenClass).First();
            Assert.That(folded.text, Is.EqualTo("2 frames hidden (UnityEngine, Game)"));
        }

        [Test]
        public void ExpandGroup_ShowsTheFoldedFrames_AndKeepsTheSelection()
        {
            var view = new DetailView { FrameFilter = new FrameFilter(hideEngineFrames: true, null) };
            LogEntry entry = Entry("boom", Trace(
                "UnityEngine.Debug:Log (object)",
                "UnityEngine.Logger:Log ()",
                "Game.Boss:Hit () (at Assets/Game/Boss.cs:88)"));
            view.Show(entry);
            TraceFrame selected = view.SelectedFrame;

            view.ExpandGroup(0);

            Assert.That(view.HiddenFrameCount, Is.EqualTo(0));
            Assert.That(view.FrameRowCount, Is.EqualTo(3));
            Assert.That(view.Query<Label>(className: DetailView.FrameHiddenClass).ToList(), Is.Empty);
            Assert.That(view.SelectedFrame, Is.SameAs(selected));
            Assert.That(view.Query<Label>(className: DetailView.FrameSelectedClass).ToList().Count, Is.EqualTo(1));
        }

        [Test]
        public void SummariseHidden_NamesUpToTwoRoots_AndAgreesOnSingular()
        {
            ParsedTrace trace = StackTraceParser.Parse(Trace(
                "UnityEngine.Debug:Log (object)",
                "System.Reflection.MethodBase:Invoke ()",
                "NUnit.Framework.Internal.Runner:Run ()"));
            var all = new FrameGroup(true, trace.Frames);
            var one = new FrameGroup(true, new[] { trace.Frames[0] });

            Assert.That(DetailView.SummariseHidden(all), Is.EqualTo("3 frames hidden (UnityEngine, System)"));
            Assert.That(DetailView.SummariseHidden(one), Is.EqualTo("1 frame hidden (UnityEngine)"));
        }

        [Test]
        public void InlineSource_DrawsASnippetUnderEveryLocatedFrame_AndKeepsTheTrailingPreviewHidden()
        {
            var asked = new List<TraceFrame>();
            var view = new DetailView
            {
                InlineSource = true,
                FrameFilter = new FrameFilter(hideEngineFrames: true, null),
                SnippetProvider = frame =>
                {
                    asked.Add(frame);
                    return new SourceSnippet(frame.FilePath, frame.Line - 1, new[] { "a", "b", "c" }, 1);
                },
            };

            view.Show(Entry("boom", Trace(
                "UnityEngine.Debug:LogError (object)",
                "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)",
                "Game.Boss:Update () (at Assets/Game/Boss.cs:88)",
                "Game.Loop:Tick ()")));

            Assert.That(view.InlinePreviewCount, Is.EqualTo(2), "one block per frame that has a location");
            Assert.That(asked.Select(f => f.Line), Is.EqualTo(new[] { 20, 88 }), "stack order, engine frame folded, frame without a location skipped");
            Assert.That(view.IsPreviewVisible, Is.False, "the trailing preview would repeat the entry frame's block");
            Assert.That(view.SelectedFrame, Is.Not.Null.And.Property("Line").EqualTo(20), "the entry frame is still the selected row");
            Assert.That(view.Query<Label>(className: SourcePreview.HighlightClass).ToList().Count, Is.EqualTo(2));
            List<VisualElement> blocks = view.Query(className: SourcePreview.InlineClass).ToList();
            Assert.That(blocks.Count, Is.EqualTo(2));
            Assert.That(blocks.All(b => b.Q<Label>(className: SourcePreview.HeaderClass).style.display == DisplayStyle.None), Is.True, "inline blocks show no path header; the frame row names the file");
        }

        [Test]
        public void InlineSource_SkipsFramesWhoseFileCannotBeRead()
        {
            var view = new DetailView
            {
                InlineSource = true,
                SnippetProvider = frame => frame.Line == 20 ? new SourceSnippet(frame.FilePath, 19, new[] { "a" }, 0) : null,
            };

            view.Show(Entry("boom", Trace("Game.Foo:Bar () (at Assets/Game/Foo.cs:20)", "Game.Gone:Away () (at Assets/Game/Gone.cs:5)")));

            Assert.That(view.InlinePreviewCount, Is.EqualTo(1));
            Assert.That(view.FrameRowCount, Is.EqualTo(3), "two frame rows plus one source block");
        }

        [Test]
        public void InlineSource_Toggle_ReRendersTheEntry_AndShowNullClearsTheBlocks()
        {
            var view = new DetailView
            {
                InlineSource = true,
                SnippetProvider = frame => new SourceSnippet(frame.FilePath, frame.Line - 1, new[] { "a", "b" }, 1),
            };
            view.Show(Entry("boom", Trace("Game.Foo:Bar () (at Assets/Game/Foo.cs:20)")));
            Assert.That(view.InlinePreviewCount, Is.EqualTo(1));

            view.InlineSource = false;
            Assert.That(view.InlinePreviewCount, Is.EqualTo(0));
            Assert.That(view.IsPreviewVisible, Is.True, "back to one preview under the selected frame");

            view.InlineSource = true;
            Assert.That(view.InlinePreviewCount, Is.EqualTo(1));
            Assert.That(view.IsPreviewVisible, Is.False);

            view.Show(null);
            Assert.That(view.InlinePreviewCount, Is.EqualTo(0));
            Assert.That(view.Query<Label>(className: SourcePreview.HighlightClass).ToList(), Is.Empty);
        }

        [Test]
        public void ShowHover_FloatsALongerSnippetInTheHost_AndShowingAnEntryHidesIt()
        {
            var host = new VisualElement();
            var asked = new List<TraceFrame>();
            var view = new DetailView
            {
                HoverHost = host,
                SnippetProvider = frame => new SourceSnippet(frame.FilePath, frame.Line - 1, new[] { "a", "b", "c" }, 1),
                HoverSnippetProvider = frame =>
                {
                    asked.Add(frame);
                    return new SourceSnippet(frame.FilePath, frame.Line - 7, Enumerable.Range(0, 15).Select(i => "line " + i).ToArray(), 7);
                },
            };
            host.Add(view);
            var entry = Entry("boom", Trace("Game.Foo:Bar () (at Assets/Game/Foo.cs:20)"));
            view.Show(entry);

            view.ShowHover(entry.Trace.Frames[0], new Vector2(30, 30));

            Assert.That(view.HoverCard, Is.Not.Null);
            Assert.That(view.HoverCard.IsShown, Is.True);
            Assert.That(view.HoverCard.parent, Is.SameAs(host), "the card floats in the host, not inside the scrolling pane");
            Assert.That(view.HoverCard.LineCount, Is.EqualTo(15), "the hover card shows the longer stretch");
            Assert.That(asked.Single().Line, Is.EqualTo(20));

            view.Show(null);
            Assert.That(view.HoverCard.IsShown, Is.False, "a new entry, or none, takes the card with it");
        }

        [Test]
        public void ShowHover_DoesNothingWithoutAProviderOrForAFrameWithoutALocation()
        {
            var host = new VisualElement();
            var view = new DetailView { HoverHost = host };
            var entry = Entry("boom", Trace("Game.Foo:Bar () (at Assets/Game/Foo.cs:20)", "Game.Loop:Tick ()"));
            view.Show(entry);

            view.ShowHover(entry.Trace.Frames[0], Vector2.zero);
            Assert.That(view.HoverCard, Is.Null, "no provider, no card");

            view.HoverSnippetProvider = frame => new SourceSnippet(frame.FilePath, 1, new[] { "x" }, 0);
            view.ShowHover(entry.Trace.Frames[1], Vector2.zero);
            Assert.That(view.HoverCard, Is.Null, "a frame without a location has nothing to show");
        }

        private static string Trace(params string[] frames)
        {
            return string.Join(Environment.NewLine, frames);
        }

        private static LogEntry Entry(string message, string stackTrace)
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, stackTrace, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
