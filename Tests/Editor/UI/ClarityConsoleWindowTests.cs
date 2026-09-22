using ClarityConsole.Capture;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ClarityConsoleWindowTests
    {
        [Test]
        public void MessageCellText_ShowsTheFirstLineFixed_AndTheWholeMessageWrapped()
        {
            const string message = "first line\r\nsecond line\n\n";

            Assert.That(ClarityConsoleWindow.MessageCellText(message, false), Is.EqualTo("first line"));
            Assert.That(ClarityConsoleWindow.MessageCellText(message, true), Is.EqualTo("first line\r\nsecond line"), "wrapped rows keep every line, minus the trailing blank ones");
            Assert.That(ClarityConsoleWindow.MessageCellText(null, true), Is.Empty);

            string huge = new string('x', ClarityConsoleWindow.MaxWrappedChars + 1500);
            string clipped = ClarityConsoleWindow.MessageCellText(huge, true);
            Assert.That(clipped, Does.StartWith(new string('x', ClarityConsoleWindow.MaxWrappedChars)));
            Assert.That(clipped, Does.EndWith("1,500 more characters; the whole message is in the detail pane."));
            Assert.That(ClarityConsoleWindow.MessageCellText(huge, false).Length, Is.EqualTo(huge.Length), "fixed rows clip by width, not by characters");
        }

        [Test]
        public void Window_BuildsItsTree_AndListsCapturedEntries()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("EditorWindow smoke test needs a graphical Editor session.");
            }

            ClarityConsoleWindow window = EditorWindow.GetWindow<ClarityConsoleWindow>();
            try
            {
                Debug.Log("[ClarityConsoleTest] window smoke");
                LogCaptureBootstrap.Capture.DrainAll();
                window.RefreshNow();

                var list = window.rootVisualElement.Q<MultiColumnListView>();
                Assert.That(list, Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<UnityEditor.UIElements.Toolbar>(), Is.Not.Null);
                Assert.That(list.itemsSource.Count, Is.EqualTo(window.ViewModel.Visible.Count));
                Assert.That(window.ViewModel.Visible.Count, Is.GreaterThan(0));
            }
            finally
            {
                window.Close();
            }
        }
    }
}
