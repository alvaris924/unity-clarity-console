using System;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class TagRuleTests
    {
        private const string SpawnerTrace =
            "UnityEngine.Debug:Log (object)\n" +
            "Game.Boss.EnemySpawner:Activate () (at Assets/Game/Enemies/EnemySpawner.cs:44)\n" +
            "Game.Loop:Tick ()";

        [Test]
        public void Contains_MatchesCaseInsensitively_AndOnlyWhenEnabledAndUsable()
        {
            var rule = new TagRule("Backend", TagMatch.Contains, "playfab");

            Assert.That(rule.Matches(Entry("Request to PlayFab timed out")), Is.True);
            Assert.That(rule.Matches(Entry("Request to Firebase timed out")), Is.False);
            Assert.That(new TagRule("Backend", TagMatch.Contains, "playfab", enabled: false).Matches(Entry("PlayFab")), Is.False);
            Assert.That(new TagRule("", TagMatch.Contains, "playfab").IsUsable, Is.False, "a rule needs a tag");
            Assert.That(new TagRule("Backend", TagMatch.Contains, "  ").IsUsable, Is.False, "and a pattern");
        }

        [Test]
        public void Regex_Matches_AndReportsABadPatternInsteadOfThrowing()
        {
            var rule = new TagRule("Backend", TagMatch.Regex, @"PlayFab|CBS\b");
            Assert.That(rule.Matches(Entry("[Net] cbs handshake")), Is.True, "regex rules ignore case too");
            Assert.That(rule.Matches(Entry("[Net] cbsx handshake")), Is.False);

            var broken = new TagRule("Backend", TagMatch.Regex, "PlayFab(");
            Assert.That(broken.Error, Is.Not.Null);
            Assert.That(broken.IsUsable, Is.False);
            Assert.That(broken.Matches(Entry("PlayFab(")), Is.False);
        }

        [TestCase("EnemySpawner", true, TestName = "Caller_ShortTypeName")]
        [TestCase("Game.Boss.EnemySpawner", true, TestName = "Caller_FullTypeName")]
        [TestCase("EnemySpawner.cs", true, TestName = "Caller_FileName")]
        [TestCase("enemyspawner", true, TestName = "Caller_IsCaseInsensitive")]
        [TestCase("Spawner", false, TestName = "Caller_NoPartialMatch")]
        [TestCase("Loop", false, TestName = "Caller_OnlyTheCallingFrameCounts")]
        public void Caller(string pattern, bool expected)
        {
            var rule = new TagRule("Spawns", TagMatch.Caller, pattern);

            Assert.That(rule.Matches(Entry("Activated: Pistol x6", SpawnerTrace)), Is.EqualTo(expected));
        }

        [Test]
        public void Caller_NeverMatchesAnEntryWithoutAStack()
        {
            Assert.That(new TagRule("Spawns", TagMatch.Caller, "EnemySpawner").Matches(Entry("no trace")), Is.False);
        }

        [Test]
        public void Describe_ReadsLikeAMenuItem()
        {
            Assert.That(new TagRule("A", TagMatch.Contains, "timeout").Describe(), Is.EqualTo("messages containing \"timeout\""));
            Assert.That(new TagRule("A", TagMatch.Regex, "x+").Describe(), Is.EqualTo("messages matching /x+/"));
            Assert.That(new TagRule("A", TagMatch.Caller, "EnemySpawner").Describe(), Is.EqualTo("messages from EnemySpawner"));
        }

        [Test]
        public void RuleSet_ResolvesTheFirstMatchingRule_InOrder()
        {
            var set = new TagRuleSet(new[]
            {
                new TagRule("Off", TagMatch.Contains, "PlayFab", enabled: false),
                new TagRule("Backend", TagMatch.Contains, "PlayFab"),
                new TagRule("Timeouts", TagMatch.Contains, "timed out"),
                null,
            });

            Assert.That(set.Rules.Count, Is.EqualTo(3), "nulls are dropped");
            Assert.That(set.Resolve(Entry("PlayFab timed out")), Is.EqualTo("Backend"), "first enabled match wins");
            Assert.That(set.Resolve(Entry("Firebase timed out")), Is.EqualTo("Timeouts"));
            Assert.That(set.Resolve(Entry("nothing")), Is.Null);
            Assert.That(TagRuleSet.Empty.IsEmpty, Is.True);
            Assert.That(TagRuleSet.Empty.Resolve(Entry("PlayFab")), Is.Null);
        }

        [Test]
        public void CallerFrame_PrefersTheProjectsOwnCode_AndShortensNames()
        {
            LogEntry entry = Entry("hit", SpawnerTrace);

            Assert.That(CallerFrame.Of(entry).TypeName, Is.EqualTo("Game.Boss.EnemySpawner"));
            Assert.That(CallerFrame.TagFor(entry), Is.EqualTo("EnemySpawner"));
            Assert.That(CallerFrame.ShortTypeName("Game.Boss.EnemySpawner/<>c__DisplayClass3_0"), Is.EqualTo("EnemySpawner"));
            Assert.That(CallerFrame.ShortTypeName("Game.Boss.EnemySpawner+Nested"), Is.EqualTo("EnemySpawner"));
            Assert.That(CallerFrame.ShortTypeName("Plain"), Is.EqualTo("Plain"));
            Assert.That(CallerFrame.ShortTypeName("Singleton`1<AssetBundleVersionManager>:"), Is.EqualTo("Singleton"), "generic arity, arguments and a stray colon go");
            Assert.That(CallerFrame.ShortTypeName("AssetBundleBase:"), Is.EqualTo("AssetBundleBase"));
            Assert.That(CallerFrame.ShortTypeName("System.Collections.Generic.List`1[T]"), Is.EqualTo("List"));
            Assert.That(CallerFrame.FileName("Assets/Game/Enemies/EnemySpawner.cs"), Is.EqualTo("EnemySpawner.cs"));
        }

        [Test]
        public void CallerFrame_FallsBackToTheFirstNonInfrastructureFrame_WithoutALocation()
        {
            LogEntry entry = Entry("hit", "UnityEngine.Debug:Log (object)\nGame.Loop:Tick ()\nUnityEditor.EditorApplication:Internal_CallUpdateFunctions ()");

            Assert.That(CallerFrame.TagFor(entry), Is.EqualTo("Loop"));
            Assert.That(CallerFrame.TagFor(Entry("hit", "UnityEngine.Debug:Log (object)")), Is.Empty, "nothing but engine frames gives no caller");
            Assert.That(CallerFrame.Of(LogEntry.Marker("Domain reloaded", DateTime.UtcNow, 0)), Is.Null);
        }

        private static LogEntry Entry(string message, string trace = "")
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, trace, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
