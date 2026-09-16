using System;
using ClarityConsole.Settings;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ClarityConsolePreferencesProviderTests
    {
        private bool _showTime;
        private bool _wrap;
        private string _theme;

        [SetUp]
        public void SetUp()
        {
            _showTime = ConsolePreferences.ShowTime;
            _wrap = ConsolePreferences.WrapMessages;
            _theme = ConsolePreferences.Theme;
        }

        [TearDown]
        public void TearDown()
        {
            ConsolePreferences.ShowTime = _showTime;
            ConsolePreferences.WrapMessages = _wrap;
            ConsolePreferences.Theme = _theme;
        }

        [Test]
        public void Page_ShowsTheCurrentPreferences()
        {
            ConsolePreferences.ShowTime = true;
            ConsolePreferences.WrapMessages = false;
            ConsolePreferences.Theme = ConsoleThemes.Paper.Id;

            var root = new VisualElement();
            Action detach = ClarityConsolePreferencesProvider.Build(root);
            try
            {
                Assert.That(root.Q<Toggle>("time").value, Is.True);
                Assert.That(root.Q<Toggle>("frame").value, Is.EqualTo(ConsolePreferences.ShowFrame));
                Assert.That(root.Q<Toggle>("wrap").value, Is.False);
                Assert.That(root.Q<DropdownField>("theme").value, Is.EqualTo(ConsoleThemes.Paper.DisplayName));
                Assert.That(root.Q<Button>("reset"), Is.Not.Null);
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
            Action detach = ClarityConsolePreferencesProvider.Build(root);
            Toggle time = root.Q<Toggle>("time");

            ConsolePreferences.ShowTime = true;
            Assert.That(time.value, Is.True, "a change from the header menu or another window shows up on the page");

            detach();
            ConsolePreferences.ShowTime = false;
            Assert.That(time.value, Is.True, "a detached page no longer listens");
        }

        [Test]
        public void ThemeDropdown_OffersEveryTheme_InRegistryOrder()
        {
            var root = new VisualElement();
            Action detach = ClarityConsolePreferencesProvider.Build(root);
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
