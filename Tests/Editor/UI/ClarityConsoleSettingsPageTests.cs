using System;
using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Settings;
using ClarityConsole.Tests.Settings;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ClarityConsoleSettingsPageTests
    {
        private PreferenceSnapshot _snapshot;

        [SetUp]
        public void SetUp()
        {
            _snapshot = PreferenceSnapshot.Capture();
        }

        [TearDown]
        public void TearDown()
        {
            _snapshot.Restore();
        }

        [Test]
        public void Page_ShowsTheCurrentPreferences()
        {
            ConsolePreferences.ShowTime = true;
            ConsolePreferences.WrapMessages = false;
            ConsolePreferences.Theme = ConsoleThemes.Paper.Id;

            var root = new VisualElement();
            Action detach = ClarityConsoleSettingsPage.Build(root);
            try
            {
                Assert.That(root.Q<Toggle>("time").value, Is.True);
                Assert.That(root.Q<Toggle>("frame").value, Is.EqualTo(ConsolePreferences.ShowFrame));
                Assert.That(root.Q<Toggle>("wrap").value, Is.False);
                Assert.That(root.Q<Toggle>("chips").value, Is.EqualTo(ConsolePreferences.ShowChannels));
                Assert.That(root.Q<DropdownField>("theme").value, Is.EqualTo(ConsoleThemes.Paper.DisplayName));
                Assert.That(root.Q<Button>("reset"), Is.Not.Null);
                Assert.That(root.Q<Button>("reset-project"), Is.Not.Null, "the shared settings keep a reset of their own");
                var textSize = root.Q<SliderInt>("text-size");
                Assert.That(textSize.value, Is.EqualTo(ConsolePreferences.TextSize));
                Assert.That(textSize.lowValue, Is.EqualTo(ConsolePreferences.MinTextSize));
                Assert.That(textSize.highValue, Is.EqualTo(ConsolePreferences.MaxTextSize));
            }
            finally
            {
                detach();
            }
        }

        [Test]
        public void Page_FollowsPreferenceChangesMadeElsewhere_UntilDetached()
        {
            ConsolePreferences.ShowTime = false;
            var root = new VisualElement();
            Action detach = ClarityConsoleSettingsPage.Build(root);
            Toggle time = root.Q<Toggle>("time");

            ConsolePreferences.ShowTime = true;
            Assert.That(time.value, Is.True, "a change from the header menu or another window shows up on the page");

            detach();
            ConsolePreferences.ShowTime = false;
            Assert.That(time.value, Is.True, "a detached page no longer listens");
        }

        [Test]
        public void Page_IsTheOnlyOne_WithPersonalSwitchesAboveTheSharedSettings_AndScrolls()
        {
            var root = new VisualElement();
            Action detach = ClarityConsoleSettingsPage.Build(root);
            try
            {
                ScrollView page = root.Q<ScrollView>("page");
                Assert.That(page, Is.Not.Null, "the settings window does not scroll a provider's page, so the page scrolls itself");

                List<string> sections = page.Query<Label>("section").ToList().Select(l => l.text).ToList();
                Assert.That(sections, Is.EqualTo(new[] { "Just for you", "Shared with the project" }));

                List<string> titles = page.contentContainer.Children().OfType<Label>()
                    .Where(l => l.name != "section" && l.style.unityFontStyleAndWeight.value == FontStyle.Bold)
                    .Select(l => l.text).ToList();
                Assert.That(titles, Is.EqualTo(new[]
                {
                    "Look", "Columns", "Behaviour",
                    "Channels", "Tags", "Watch rows", "Stack frames", "Ignored messages",
                }));
                Assert.That(ClarityConsoleSettingsPage.Path, Does.StartWith("Project/"), "not on the Preferences window");
            }
            finally
            {
                detach();
            }
        }

        [Test]
        public void ThemeDropdown_OffersEveryTheme_InRegistryOrder()
        {
            var root = new VisualElement();
            Action detach = ClarityConsoleSettingsPage.Build(root);
            try
            {
                var dropdown = root.Q<DropdownField>("theme");
                Assert.That(dropdown.choices.Count, Is.EqualTo(ConsoleThemes.All.Count));
                for (int i = 0; i < ConsoleThemes.All.Count; i++)
                {
                    Assert.That(dropdown.choices[i], Is.EqualTo(ConsoleThemes.All[i].DisplayName));
                }
            }
            finally
            {
                detach();
            }
        }
    }
}
