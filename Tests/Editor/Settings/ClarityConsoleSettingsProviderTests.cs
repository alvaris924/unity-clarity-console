using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Settings;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.Settings
{
    internal sealed class ClarityConsoleSettingsProviderTests
    {
        [Test]
        public void Page_ScrollsAndKeepsTagsNextToChannels()
        {
            var root = new VisualElement();

            ClarityConsoleSettingsProvider.Build(root);

            ScrollView page = root.Q<ScrollView>("page");
            Assert.That(page, Is.Not.Null, "the settings window does not scroll a provider's page, so the page scrolls itself");
            Assert.That(page.mode, Is.EqualTo(ScrollViewMode.Vertical));

            List<string> titles = page.contentContainer.Children().OfType<Label>()
                .Where(l => l.style.unityFontStyleAndWeight.value == UnityEngine.FontStyle.Bold)
                .Select(l => l.text).ToList();
            Assert.That(titles, Is.EqualTo(new[] { "Channels", "Tags", "Watch rows", "Stack frames", "Ignored messages" }));
            Assert.That(root.Q<Button>("preferences"), Is.Not.Null, "the per-user page is one click away");
        }
    }
}
