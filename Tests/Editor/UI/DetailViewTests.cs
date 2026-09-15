using System;
using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
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
        public void Show_Marker_HasNoRows()
        {
            var view = new DetailView();

            view.Show(LogEntry.Marker("Entered Play mode", DateTime.UtcNow, 0));

            Assert.That(view.FrameRowCount, Is.EqualTo(0));
            Assert.That(view.Q<TextField>().value, Is.EqualTo("Entered Play mode"));
        }

        private static LogEntry Entry(string message, string stackTrace)
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, stackTrace, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
