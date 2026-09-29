using System;
using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class SessionReportViewTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Show_DrawsTheVerdict_Tiles_FirstError_AndRepeatedLines()
        {
            var view = new SessionReportView();

            view.Show(SessionReport.Build(Run().Entries, 1));

            Label verdict = view.Q<Label>(className: SessionReportView.VerdictClass);
            Assert.That(verdict.text, Is.EqualTo("2 errors. The first came 0:30 in."));
            Assert.That(verdict.ClassListContains("cc-msg-error"), Is.True);
            Assert.That(view.Query(className: SessionReportView.TileClass).ToList().Count, Is.EqualTo(3));
            Assert.That(view.Q<Button>("show-first-error"), Is.Not.Null);
            Assert.That(view.Q<Button>("flow-first-error"), Is.Not.Null, "the error has a stack to follow");

            var sections = view.Query<Label>(className: SessionReportView.SectionClass).ToList().Select(l => l.text).ToList();
            Assert.That(sections, Is.EqualTo(new[] { "First error", "Most repeated errors", "Chattiest messages" }), "no channel section without channels");
            Assert.That(view.Query(className: SessionReportView.LineClass).ToList().Count, Is.EqualTo(2));
        }

        [Test]
        public void Show_Null_SaysThereIsNoSessionYet()
        {
            var view = new SessionReportView();

            view.Show(null);

            Assert.That(view.Q<Label>().text, Does.StartWith("No Play session yet"));
            Assert.That(view.Query(className: SessionReportView.TileClass).ToList(), Is.Empty);
        }

        [Test]
        public void Describe_MentionsFrameRate_AndARunThatHasNotEnded()
        {
            var store = new LogStore { CurrentSession = 1 };
            store.Append(LogEntry.Marker(LogEntry.EnteredPlayModeMarker + "1", Start, 100));
            store.Append(new LogEntry(LogEntryKind.Log, LogSeverity.Log, "tick", string.Empty, Start.AddSeconds(10), 700, 1, true, ObjectRef.None));

            string text = SessionReportView.Describe(SessionReport.Build(store.Entries, 1));

            Assert.That(text, Does.StartWith("Ran 0:10, about 60 fps over 600 frames."));
            Assert.That(text, Does.Contain("Still running"));
        }

        /// <summary>One run: the same error twice with a stack, 60 seconds long.</summary>
        private static LogStore Run()
        {
            var store = new LogStore { CurrentSession = 1 };
            store.Append(LogEntry.Marker(LogEntry.EnteredPlayModeMarker + "1", Start, 0));
            string stack = "Game.Boss:Hit () (at Assets/Game/Boss.cs:88)\n";
            store.Append(new LogEntry(LogEntryKind.Log, LogSeverity.Error, "boss lost target", stack, Start.AddSeconds(30), 1800, 1, true, ObjectRef.None));
            store.Append(new LogEntry(LogEntryKind.Log, LogSeverity.Error, "boss lost target", stack, Start.AddSeconds(40), 2400, 1, true, ObjectRef.None));
            store.Append(LogEntry.Marker(LogEntry.ExitingPlayModeMarker, Start.AddSeconds(60), 3600));
            return store;
        }
    }
}
