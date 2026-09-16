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
        private bool _wrapMessages;
        private bool _showTime;
        private bool _showFrame;

        [SetUp]
        public void SetUp()
        {
            _errorPause = ConsolePreferences.ErrorPause;
            _clearOnPlay = ConsolePreferences.ClearOnPlay;
            _clearOnRecompile = ConsolePreferences.ClearOnRecompile;
            _clearOnBuild = ConsolePreferences.ClearOnBuild;
            _wrapMessages = ConsolePreferences.WrapMessages;
            _showTime = ConsolePreferences.ShowTime;
            _showFrame = ConsolePreferences.ShowFrame;
        }

        [TearDown]
        public void TearDown()
        {
            ConsolePreferences.ErrorPause = _errorPause;
            ConsolePreferences.ClearOnPlay = _clearOnPlay;
            ConsolePreferences.ClearOnRecompile = _clearOnRecompile;
            ConsolePreferences.ClearOnBuild = _clearOnBuild;
            ConsolePreferences.WrapMessages = _wrapMessages;
            ConsolePreferences.ShowTime = _showTime;
            ConsolePreferences.ShowFrame = _showFrame;
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
        public void WrapMessages_IsOffByDefault_RoundTrips_AndResets()
        {
            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.WrapMessages, Is.False);

            ConsolePreferences.WrapMessages = true;
            Assert.That(ConsolePreferences.WrapMessages, Is.True);
            Assert.That(ConsolePreferences.ClearOnPlay, Is.False, "wrap must not touch the other switches");

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.WrapMessages, Is.False);
        }

        [Test]
        public void InlineSource_IsOnByDefault_AndRoundTrips()
        {
            bool original = ConsolePreferences.InlineSource;
            try
            {
                ConsolePreferences.ResetToDefaults();
                Assert.That(ConsolePreferences.InlineSource, Is.True);

                ConsolePreferences.InlineSource = false;
                Assert.That(ConsolePreferences.InlineSource, Is.False);

                ConsolePreferences.ResetToDefaults();
                Assert.That(ConsolePreferences.InlineSource, Is.True);
            }
            finally
            {
                ConsolePreferences.InlineSource = original;
            }
        }

        [Test]
        public void TimeAndFrameColumns_AreOffByDefault_AndRoundTripIndependently()
        {
            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.ShowTime, Is.False);
            Assert.That(ConsolePreferences.ShowFrame, Is.False);

            ConsolePreferences.ShowFrame = true;
            Assert.That(ConsolePreferences.ShowFrame, Is.True);
            Assert.That(ConsolePreferences.ShowTime, Is.False);

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.ShowFrame, Is.False);
        }

        [Test]
        public void Theme_RoundTrips_AndResetsToEmpty()
        {
            string original = ConsolePreferences.Theme;
            try
            {
                ConsolePreferences.Theme = "scifi";
                Assert.That(ConsolePreferences.Theme, Is.EqualTo("scifi"));

                ConsolePreferences.ResetToDefaults();
                Assert.That(ConsolePreferences.Theme, Is.Empty);
            }
            finally
            {
                ConsolePreferences.Theme = original;
            }
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
