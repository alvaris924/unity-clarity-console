using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class CallFlowViewTests
    {
        [Test]
        public void Show_DrawsOneCardPerCall_JoinedByArrows_WithTheSourceUnderEach()
        {
            var asked = new List<int>();
            var view = new CallFlowView
            {
                SnippetProvider = frame =>
                {
                    asked.Add(frame.Line);
                    return new SourceSnippet(frame.FilePath, frame.Line - 1, new[] { "a", "b", "c" }, 1);
                },
            };

            view.Show(Entry(LogSeverity.Exception, "NullReferenceException: boom\nsecond line", Trace(
                "Game.Spawner:Init () (at Assets/Game/Spawner.cs:40)",
                "Game.Spawner:Spawn () (at Assets/Game/Spawner.cs:22)",
                "Game.Loop:Update ()")));

            List<VisualElement> cards = view.Query(className: CallFlowView.CardClass).ToList();
            Assert.That(cards.Count, Is.EqualTo(3));
            Assert.That(view.Query(className: CallFlowView.ConnectorClass).ToList().Count, Is.EqualTo(2), "one arrow between each pair of steps");
            Assert.That(asked, Is.EqualTo(new[] { 22, 40 }), "outermost first, and no source for a frame without a location");
            Assert.That(view.Query(className: SourcePreview.InlineClass).ToList().Count, Is.EqualTo(2));

            Assert.That(cards[0].Q<Label>(className: "cc-flow-name").text, Is.EqualTo("Loop.Update"));
            Assert.That(cards[2].ClassListContains(CallFlowView.CardOriginClass), Is.True);
            Assert.That(cards[2].ClassListContains(CallFlowView.CardFailureClass), Is.True);
            Assert.That(cards[2].ClassListContains(CallFlowView.CardEntryClass), Is.True);
            Assert.That(cards[2].Q<Label>(className: CallFlowView.NoteClass).text, Is.EqualTo("threw here"));
            Assert.That(cards[0].ClassListContains(CallFlowView.CardLinkClass), Is.False, "a frame without a location cannot be opened");
            Assert.That(cards[1].ClassListContains(CallFlowView.CardLinkClass), Is.True);

            Assert.That(view.MessageText, Is.EqualTo("NullReferenceException: boom"));
            Assert.That(view.SummaryText, Does.StartWith("3 calls,"));
        }

        [Test]
        public void FoldedRun_IsOneStep_UntilItIsUnfolded()
        {
            var view = new CallFlowView { FrameFilter = new FrameFilter(hideEngineFrames: true, null) };
            view.Show(Entry(LogSeverity.Log, "hello", Trace(
                "UnityEngine.Debug:Log (object)",
                "Game.Boss:Hit () (at Assets/Game/Boss.cs:88)",
                "UnityEngine.Events.InvokableCall:Invoke ()",
                "UnityEngine.Events.UnityEvent:Invoke ()")));

            Assert.That(view.Query(className: CallFlowView.CardClass).ToList().Count, Is.EqualTo(1));
            List<Label> folded = view.Query<Label>(className: CallFlowView.FoldedClass).ToList();
            Assert.That(folded.Select(l => l.text), Is.EqualTo(new[] { "2 frames hidden (UnityEngine)", "1 frame hidden (UnityEngine)" }));
            Assert.That(view.SummaryText, Does.StartWith("1 call, 3 frames folded"));

            view.ExpandGroup(view.Steps[0].GroupIndex);

            Assert.That(view.Query(className: CallFlowView.CardClass).ToList().Count, Is.EqualTo(3));
            Assert.That(view.Query<Label>(className: CallFlowView.FoldedClass).ToList().Count, Is.EqualTo(1));
            Assert.That(view.Steps.Last().IsOrigin, Is.True, "Debug.Log stays folded and is still where it was logged");
        }

        [Test]
        public void Show_AnotherEntry_ForgetsWhatWasUnfolded()
        {
            var view = new CallFlowView { FrameFilter = new FrameFilter(hideEngineFrames: true, null) };
            string trace = Trace("UnityEngine.Debug:Log (object)", "Game.Boss:Hit () (at Assets/Game/Boss.cs:88)");
            LogEntry first = Entry(LogSeverity.Log, "a", trace);
            view.Show(first);
            view.ExpandGroup(0);
            Assert.That(view.Query<Label>(className: CallFlowView.FoldedClass).ToList(), Is.Empty);

            view.Show(first);
            Assert.That(view.Query<Label>(className: CallFlowView.FoldedClass).ToList(), Is.Empty, "the same entry keeps its unfolded runs");

            view.Show(Entry(LogSeverity.Log, "b", trace));
            Assert.That(view.Query<Label>(className: CallFlowView.FoldedClass).ToList().Count, Is.EqualTo(1));
        }

        [Test]
        public void Show_NullOrAnEntryWithoutAStack_SaysSo()
        {
            var view = new CallFlowView();

            view.Show(null);
            Assert.That(view.Steps, Is.Empty);
            Assert.That(view.SummaryText, Does.Contain("Show flow"));

            view.Show(Entry(LogSeverity.Error, "boom", string.Empty));
            Assert.That(view.Steps, Is.Empty);
            Assert.That(view.SummaryText, Is.EqualTo("This entry has no stack trace to follow."));
            Assert.That(view.Query(className: CallFlowView.CardClass).ToList(), Is.Empty);
        }

        [Test]
        public void NoteText_NamesTheOrigin_AndCompilerGeneratedCalls()
        {
            List<FlowStep> steps = CallFlow.Build(Entry(LogSeverity.Warning, "w", Trace(
                "Game.Spawner:Log () (at Assets/Spawner.cs:9)",
                "Game.Spawner/<SpawnWave>d__4:MoveNext () (at Assets/Spawner.cs:30)")), null);

            Assert.That(CallFlowView.NoteText(steps[0], LogSeverity.Warning), Is.EqualTo("resumed"));
            Assert.That(CallFlowView.NoteText(steps[1], LogSeverity.Warning), Is.EqualTo("logged here"));
            Assert.That(CallFlowView.NoteText(steps[1], LogSeverity.Exception), Is.EqualTo("threw here"));
        }

        [Test]
        public void Window_OpensOnTheEntry_InTheConsoleTheme()
        {
            if (UnityEngine.Application.isBatchMode)
            {
                Assert.Ignore("EditorWindow smoke test needs a graphical Editor session.");
            }

            LogEntry entry = Entry(LogSeverity.Error, "boom", Trace("Game.Foo:Bar () (at Assets/Game/Foo.cs:20)"));
            CallFlowWindow window = CallFlowWindow.Open(entry);
            try
            {
                Assert.That(window.View, Is.Not.Null);
                Assert.That(window.View.Entry, Is.SameAs(entry));
                Assert.That(window.rootVisualElement.ClassListContains("cc-root"), Is.True);
                Assert.That(window.View.Query(className: CallFlowView.CardClass).ToList().Count, Is.EqualTo(1));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void FirstLine_TakesTheTextBeforeTheFirstBreak()
        {
            Assert.That(CallFlowView.FirstLine("one\r\ntwo"), Is.EqualTo("one"));
            Assert.That(CallFlowView.FirstLine("single"), Is.EqualTo("single"));
            Assert.That(CallFlowView.FirstLine(null), Is.Empty);
        }

        private static LogEntry Entry(LogSeverity severity, string message, string stackTrace)
        {
            return new LogEntry(LogEntryKind.Log, severity, message, stackTrace, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }

        private static string Trace(params string[] lines)
        {
            return string.Join("\n", lines) + "\n";
        }
    }
}
