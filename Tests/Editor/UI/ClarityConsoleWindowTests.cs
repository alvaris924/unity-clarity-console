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
