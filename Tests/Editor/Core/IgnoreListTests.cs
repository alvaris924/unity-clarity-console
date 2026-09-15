using System;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class IgnoreListTests
    {
        [Test]
        public void Message_MatchesExactlyAndCaseSensitively()
        {
            var rule = new IgnoreRule(IgnoreMatch.Message, "Shader warning: unused variable");

            Assert.That(rule.Matches(Entry("Shader warning: unused variable")), Is.True);
            Assert.That(rule.Matches(Entry("shader warning: unused variable")), Is.False);
            Assert.That(rule.Matches(Entry("Shader warning: unused variable (again)")), Is.False);
        }

        [Test]
        public void Contains_IsCaseInsensitive()
        {
            var rule = new IgnoreRule(IgnoreMatch.Contains, "unused variable");

            Assert.That(rule.Matches(Entry("Shader warning: UNUSED VARIABLE x")), Is.True);
            Assert.That(rule.Matches(Entry("all good")), Is.False);
        }

        [Test]
        public void Channel_MatchesTheEntrysChannel()
        {
            var rule = new IgnoreRule(IgnoreMatch.Channel, "Noisy");
            LogEntry entry = Entry("[Noisy] tick");
            entry.Channel = "Noisy";

            Assert.That(rule.Matches(entry), Is.True);
            Assert.That(rule.Matches(Entry("[Noisy] tick")), Is.False, "an entry with no channel assigned does not match");
        }

        [Test]
        public void Regex_MatchesAndReportsABadPattern()
        {
            Assert.That(new IgnoreRule(IgnoreMatch.Regex, @"^tick \d+$").Matches(Entry("tick 42")), Is.True);

            var broken = new IgnoreRule(IgnoreMatch.Regex, "[unclosed");
            Assert.That(broken.Error, Is.Not.Null);
            Assert.That(broken.IsUsable, Is.False);
            Assert.That(broken.Matches(Entry("[unclosed")), Is.False);
        }

        [Test]
        public void DisabledOrEmptyRules_SilenceNothing()
        {
            Assert.That(new IgnoreRule(IgnoreMatch.Contains, "tick", enabled: false).Matches(Entry("tick")), Is.False);
            Assert.That(new IgnoreRule(IgnoreMatch.Contains, string.Empty).Matches(Entry("tick")), Is.False);
        }

        [Test]
        public void Markers_AreNeverIgnored()
        {
            var rule = new IgnoreRule(IgnoreMatch.Contains, "Play");

            Assert.That(rule.Matches(LogEntry.Marker("Entered Play mode", DateTime.UtcNow, 0)), Is.False);
        }

        [Test]
        public void List_IgnoresIfAnyRuleMatches_AndDropsUnusableRules()
        {
            var list = new IgnoreList(new[]
            {
                new IgnoreRule(IgnoreMatch.Contains, "tick"),
                new IgnoreRule(IgnoreMatch.Regex, "[unclosed"),
                new IgnoreRule(IgnoreMatch.Message, "silent", enabled: false),
                null,
            });

            Assert.That(list.Rules.Count, Is.EqualTo(1));
            Assert.That(list.ShouldIgnore(Entry("tick 42")), Is.True);
            Assert.That(list.ShouldIgnore(Entry("silent")), Is.False);
            Assert.That(list.IsEmpty, Is.False);
        }

        [Test]
        public void EmptyList_IgnoresNothing()
        {
            Assert.That(IgnoreList.Empty.IsEmpty, Is.True);
            Assert.That(IgnoreList.Empty.ShouldIgnore(Entry("anything")), Is.False);
            Assert.That(new IgnoreList(null).ShouldIgnore(Entry("anything")), Is.False);
        }

        [Test]
        public void Describe_ReadsAsASentence()
        {
            Assert.That(new IgnoreRule(IgnoreMatch.Message, "boom").Describe(), Is.EqualTo("Message is \"boom\""));
            Assert.That(new IgnoreRule(IgnoreMatch.Contains, "boom").Describe(), Is.EqualTo("Message contains \"boom\""));
            Assert.That(new IgnoreRule(IgnoreMatch.Channel, "Net").Describe(), Is.EqualTo("Channel is Net"));
            Assert.That(new IgnoreRule(IgnoreMatch.Regex, "^a").Describe(), Is.EqualTo("Message matches /^a/"));
        }

        private static LogEntry Entry(string message)
        {
            return new LogEntry(LogEntryKind.Log, LogSeverity.Log, message, string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
