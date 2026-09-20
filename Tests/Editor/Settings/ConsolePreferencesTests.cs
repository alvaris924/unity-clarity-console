using System.Collections.Generic;
using System.Reflection;
using ClarityConsole.Settings;
using NUnit.Framework;

namespace ClarityConsole.Tests.Settings
{
    /// <summary>
    /// Every test here edits the machine-wide EditorPrefs, so the fixture puts all of them back afterwards:
    /// a reset that leaked out of a test run used to wipe the developer's theme in every open project.
    /// </summary>
    internal sealed class ConsolePreferencesTests
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
        public void TextSize_DefaultsTo11_Clamps_AndResets()
        {
            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.TextSize, Is.EqualTo(ConsolePreferences.DefaultTextSize));

            ConsolePreferences.TextSize = 13;
            Assert.That(ConsolePreferences.TextSize, Is.EqualTo(13));

            ConsolePreferences.TextSize = 40;
            Assert.That(ConsolePreferences.TextSize, Is.EqualTo(ConsolePreferences.MaxTextSize), "clamped to the largest size");

            ConsolePreferences.TextSize = 2;
            Assert.That(ConsolePreferences.TextSize, Is.EqualTo(ConsolePreferences.MinTextSize), "clamped to the smallest size");

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.TextSize, Is.EqualTo(ConsolePreferences.DefaultTextSize));
        }

        [Test]
        public void ShowDomainReloads_IsOffByDefault_AndRoundTrips()
        {
            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.ShowDomainReloads, Is.False);

            ConsolePreferences.ShowDomainReloads = true;
            Assert.That(ConsolePreferences.ShowDomainReloads, Is.True);

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.ShowDomainReloads, Is.False);
        }

        [Test]
        public void InlineSource_IsOnByDefault_AndRoundTrips()
        {
            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.InlineSource, Is.True);

            ConsolePreferences.InlineSource = false;
            Assert.That(ConsolePreferences.InlineSource, Is.False);

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.InlineSource, Is.True);
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
            ConsolePreferences.Theme = "scifi";
            Assert.That(ConsolePreferences.Theme, Is.EqualTo("scifi"));

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.Theme, Is.Empty);
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

        /// <summary>
        /// The guard the fixture relies on: after a reset wipes the keys, restoring a snapshot brings every
        /// preference back, the theme and text size included, not just the switches.
        /// </summary>
        [Test]
        public void Snapshot_RestoresEveryPreference_AfterAReset()
        {
            var expected = new Dictionary<PropertyInfo, object>();
            foreach (PropertyInfo property in PreferenceSnapshot.Properties())
            {
                expected[property] = OffDefault(property.GetValue(null));
                property.SetValue(null, expected[property]);
            }

            PreferenceSnapshot changed = PreferenceSnapshot.Capture();
            Assert.That(changed.Names, Is.SupersetOf(new[] { "Theme", "TextSize", "InlineSource", "ShowDomainReloads", "WrapMessages", "ErrorPause" }));

            ConsolePreferences.ResetToDefaults();
            Assert.That(ConsolePreferences.Theme, Is.Empty, "the reset really wiped the keys");
            Assert.That(ConsolePreferences.TextSize, Is.EqualTo(ConsolePreferences.DefaultTextSize));

            changed.Restore();

            foreach (KeyValuePair<PropertyInfo, object> value in expected)
            {
                Assert.That(value.Key.GetValue(null), Is.EqualTo(value.Value), value.Key.Name + " came back");
            }
        }

        private static object OffDefault(object value)
        {
            switch (value)
            {
                case bool flag:
                    return !flag;
                case int number:
                    // Both inside the text-size range, so the clamp does not fold them back.
                    return number == 12 ? 13 : 12;
                case string text:
                    return text == "scifi" ? "paper" : "scifi";
                default:
                    Assert.Fail("unexpected preference type " + value?.GetType());
                    return value;
            }
        }
    }
}
