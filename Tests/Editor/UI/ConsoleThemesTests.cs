using System.Linq;
using ClarityConsole.UI;
using NUnit.Framework;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ConsoleThemesTests
    {
        [Test]
        public void EveryTheme_HasAUniqueId_AndAStyleSheetThatLoads()
        {
            Assert.That(ConsoleThemes.All.Select(t => t.Id).Distinct().Count(), Is.EqualTo(ConsoleThemes.All.Count));

            foreach (ConsoleTheme theme in ConsoleThemes.All)
            {
                Assert.That(theme.Load(), Is.Not.Null, theme.DisplayName + " at " + theme.StyleSheetPath);
            }
        }

        [Test]
        public void Native_IsTheDefault_SoTheConsoleLooksLikeTheEditorUntilAsked()
        {
            Assert.That(ConsoleThemes.Default, Is.SameAs(ConsoleThemes.Native));
            Assert.That(ConsoleThemes.All[0], Is.SameAs(ConsoleThemes.Native));
        }

        [TestCase("scifi")]
        [TestCase("SciFi")]
        [TestCase("SCIFI")]
        public void Find_IsCaseInsensitive(string id)
        {
            Assert.That(ConsoleThemes.Find(id), Is.SameAs(ConsoleThemes.SciFi));
        }

        [Test]
        public void Ember_IsRegistered_AndLoads()
        {
            Assert.That(ConsoleThemes.Find("ember"), Is.SameAs(ConsoleThemes.Ember));
            Assert.That(ConsoleThemes.Ember.Load(), Is.Not.Null);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("no-such-theme")]
        public void Find_FallsBackToTheDefault(string id)
        {
            Assert.That(ConsoleThemes.Find(id), Is.SameAs(ConsoleThemes.Default));
        }

        [Test]
        public void ThemesShipTheFontsTheyReference()
        {
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Packages/com.alvaris.clarity-console/Editor/UI/Fonts/JetBrainsMono-Regular.ttf"), Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Packages/com.alvaris.clarity-console/Editor/UI/Fonts/JetBrainsMono-Bold.ttf"), Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Packages/com.alvaris.clarity-console/Editor/UI/Themes/scanlines.png"), Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Packages/com.alvaris.clarity-console/Editor/UI/Themes/glow-frame.png"), Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Packages/com.alvaris.clarity-console/Editor/UI/Themes/ember-row-glow.png"), Is.Not.Null);
        }
    }
}
