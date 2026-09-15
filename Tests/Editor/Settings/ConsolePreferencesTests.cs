using ClarityConsole.Settings;
using NUnit.Framework;

namespace ClarityConsole.Tests.Settings
{
    internal sealed class ConsolePreferencesTests
    {
        private bool _errorPause;
        private bool _clearOnPlay;
        private bool _clearOnRecompile;
        private bool _clearOnBuild;

        [SetUp]
        public void SetUp()
        {
            _errorPause = ConsolePreferences.ErrorPause;
            _clearOnPlay = ConsolePreferences.ClearOnPlay;
            _clearOnRecompile = ConsolePreferences.ClearOnRecompile;
            _clearOnBuild = ConsolePreferences.ClearOnBuild;
        }

        [TearDown]
        public void TearDown()
        {
            ConsolePreferences.ErrorPause = _errorPause;
            ConsolePreferences.ClearOnPlay = _clearOnPlay;
            ConsolePreferences.ClearOnRecompile = _clearOnRecompile;
            ConsolePreferences.ClearOnBuild = _clearOnBuild;
        }

        [Test]
        public void EachPreference_RoundTripsIndependently()
        {
            ConsolePreferences.ResetToDefaults();

            ConsolePreferences.ClearOnPlay = true;

            Assert.That(ConsolePreferences.ClearOnPlay, Is.True);
            Assert.That(ConsolePreferences.ClearOnRecompile, Is.False);
            Assert.That(ConsolePreferences.ClearOnBuild, Is.False);
            Assert.That(ConsolePreferences.ErrorPause, Is.False);
        }

        [Test]
        public void Defaults_AreAllOff()
        {
            ConsolePreferences.ResetToDefaults();

            Assert.That(ConsolePreferences.ErrorPause, Is.False);
            Assert.That(ConsolePreferences.ClearOnPlay, Is.False);
            Assert.That(ConsolePreferences.ClearOnRecompile, Is.False);
            Assert.That(ConsolePreferences.ClearOnBuild, Is.False);
        }

        [Test]
        public void Changed_FiresOnARealChange_AndOnReset()
        {
            ConsolePreferences.ResetToDefaults();
            int changes = 0;
            void OnChanged() => changes++;
            ConsolePreferences.Changed += OnChanged;

            try
            {
                ConsolePreferences.ErrorPause = true;
                Assert.That(changes, Is.EqualTo(1));

                ConsolePreferences.ErrorPause = true;
                Assert.That(changes, Is.EqualTo(1), "setting the same value again changes nothing");

                ConsolePreferences.ErrorPause = false;
                Assert.That(changes, Is.EqualTo(2));

                ConsolePreferences.ResetToDefaults();
                Assert.That(changes, Is.EqualTo(3));
            }
            finally
            {
                ConsolePreferences.Changed -= OnChanged;
            }
        }
    }
}
