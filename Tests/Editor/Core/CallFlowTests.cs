using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class CallFlowTests
    {
        [Test]
        public void Build_ReadsFromTheOutermostCallDownToTheLineThatLogged()
        {
            LogEntry entry = Entry(LogSeverity.Error, Trace(
                "Game.Enemies.Spawner:Init () (at Assets/Game/Spawner.cs:40)",
                "Game.Enemies.Spawner:Spawn () (at Assets/Game/Spawner.cs:22)",
                "Game.Loop:Update () (at Assets/Game/Loop.cs:9)"));

            List<FlowStep> steps = CallFlow.Build(entry, null);

            Assert.That(steps.Select(s => s.Name.ToString()), Is.EqualTo(new[] { "Loop.Update", "Spawner.Spawn", "Spawner.Init" }));
            Assert.That(steps.All(s => s.Kind == FlowStepKind.Call), Is.True);
            Assert.That(steps.Select(s => s.IsOrigin), Is.EqualTo(new[] { false, false, true }), "the innermost frame is where it happened");
            Assert.That(steps[2].IsEntry, Is.True, "the first user frame Unity printed is the one the detail pane opens");
        }

        [Test]
        public void Build_FoldsInfrastructureRuns_AndUnfoldsTheOnesAsked()
        {
            LogEntry entry = Entry(LogSeverity.Log, Trace(
                "UnityEngine.Debug:Log (object)",
                "Game.Boss:Hit () (at Assets/Game/Boss.cs:88)",
                "UnityEngine.Events.InvokableCall:Invoke ()",
                "UnityEngine.Events.UnityEvent:Invoke ()"));
            var filter = new FrameFilter(hideEngineFrames: true, null);

            List<FlowStep> steps = CallFlow.Build(entry, filter);

            Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[] { FlowStepKind.Folded, FlowStepKind.Call, FlowStepKind.Folded }));
            Assert.That(steps[0].Frames.Select(f => f.MethodName), Is.EqualTo(new[] { "Invoke", "Invoke" }));
            Assert.That(steps[0].Frames[0].TypeName, Is.EqualTo("UnityEngine.Events.UnityEvent"), "a folded run is outermost first too");
            Assert.That(steps[2].IsOrigin, Is.True, "Debug.Log is still where the message was logged");
            Assert.That(steps[1].IsEntry, Is.True);

            List<FlowStep> unfolded = CallFlow.Build(entry, filter, new HashSet<int> { steps[0].GroupIndex });

            Assert.That(unfolded.Select(s => s.Kind), Is.EqualTo(new[] { FlowStepKind.Call, FlowStepKind.Call, FlowStepKind.Call, FlowStepKind.Folded }));
            Assert.That(unfolded[0].Name.ToString(), Is.EqualTo("UnityEvent.Invoke"));
        }

        [Test]
        public void Build_AnErrorMadeOnlyOfInfrastructure_StartsUnfolded_ButAPlainLogFolds()
        {
            string trace = Trace(
                "UnityEngine.UIElements.VisualElement:Repaint ()",
                "UnityEditor.UIElements.EditorPanel:UpdateForRepaint ()");
            var filter = new FrameFilter(hideEngineFrames: true, null);

            Assert.That(CallFlow.Build(Entry(LogSeverity.Exception, trace), filter).Count, Is.EqualTo(2));
            Assert.That(CallFlow.Build(Entry(LogSeverity.Log, trace), filter).Single().Kind, Is.EqualTo(FlowStepKind.Folded));
        }

        [Test]
        public void Build_HasNothingForMarkersOrEntriesWithoutAStack()
        {
            Assert.That(CallFlow.Build(LogEntry.Marker("Entered Play mode", DateTime.UtcNow, 0), null), Is.Empty);
            Assert.That(CallFlow.Build(Entry(LogSeverity.Error, string.Empty), null), Is.Empty);
            Assert.That(CallFlow.Build(null, null), Is.Empty);
            Assert.That(CallFlow.CanShow(Entry(LogSeverity.Error, string.Empty)), Is.False);
            Assert.That(CallFlow.CanShow(Entry(LogSeverity.Error, "Game.A:B ()")), Is.True);
        }

        [TestCase("Game.Enemies.Spawner/<SpawnWave>d__4:MoveNext () (at Assets/Spawner.cs:30)", "Spawner.SpawnWave", CallNote.Resumed)]
        [TestCase("Game.Enemies.Spawner+<LoadAsync>d__7.MoveNext () (at Assets/Spawner.cs:51)", "Spawner.LoadAsync", CallNote.Resumed)]
        [TestCase("Game.Ui.Menu/<>c__DisplayClass3_0:<Start>b__0 () (at Assets/Menu.cs:12)", "Menu.Start", CallNote.Lambda)]
        [TestCase("Game.Ui.Menu/<>c:<Awake>b__2_0 () (at Assets/Menu.cs:8)", "Menu.Awake", CallNote.Lambda)]
        [TestCase("Game.Ui.Menu:<Open>g__Fade|5_0 () (at Assets/Menu.cs:20)", "Menu.Open.Fade", CallNote.LocalFunction)]
        [TestCase("Game.Ui.Menu/Tab:Select () (at Assets/Menu.cs:70)", "Tab.Select", CallNote.None)]
        [TestCase("System.Collections.Generic.List`1[System.Int32]:Add (int)", "List`1[System.Int32].Add", CallNote.None)]
        [TestCase("Boot:Main ()", "Boot.Main", CallNote.None)]
        public void CallName_ReadsCompilerGeneratedNamesAsTheMethodTheyCameFrom(string line, string expected, CallNote note)
        {
            CallName name = CallName.For(StackTraceParser.ParseLine(line));

            Assert.That(name.ToString(), Is.EqualTo(expected));
            Assert.That(name.Note, Is.EqualTo(note));
        }

        private static LogEntry Entry(LogSeverity severity, string stackTrace)
        {
            return new LogEntry(LogEntryKind.Log, severity, "boom", stackTrace, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }

        private static string Trace(params string[] lines)
        {
            return string.Join("\n", lines) + "\n";
        }
    }
}
