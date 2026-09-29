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
        public void SharedSections_KeepTagsNextToChannels()
        {
            var page = new VisualElement();

            ClarityConsoleSettingsProvider.BuildShared(page);

            List<string> titles = page.Children().OfType<Label>()
                .Where(l => l.style.unityFontStyleAndWeight.value == UnityEngine.FontStyle.Bold)
                .Select(l => l.text).ToList();
            Assert.That(titles, Is.EqualTo(new[] { "Channels", "Tags", "Watch rows", "Stack frames", "Ignored messages" }));
            Assert.That(page.Q<Button>("reset-project"), Is.Not.Null);
        }
    }
}
