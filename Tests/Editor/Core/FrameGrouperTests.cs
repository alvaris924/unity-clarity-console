using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class FrameGrouperTests
    {
        private static readonly string[] TraceLines =
        {
            "UnityEngine.Debug:Log (object)",
            "ClarityConsole.Capture.LogHandlerWrapper:LogFormat () (at Packages/x/LogHandlerWrapper.cs:27)",
            "Game.Logging.Log:Info (string) (at Assets/Game/Logging/Log.cs:10)",
            "Game.Boss.BossController:TakeDamage () (at Assets/Game/BossController.cs:88)",
            "UnityEngine.MonoBehaviour:Update ()",
        };

        private static ParsedTrace Trace()
        {
            return StackTraceParser.Parse(string.Join("\n", TraceLines));
        }

        [Test]
        public void Group_WithNoFilter_KeepsOneVisibleRun()
        {
            var groups = FrameGrouper.Group(Trace(), FrameFilter.ShowEverything);

            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].IsNoise, Is.False);
            Assert.That(groups[0].Count, Is.EqualTo(5));
        }

        [Test]
        public void Group_FoldsEngineRuns_AroundTheUsersCode()
        {
            var groups = FrameGrouper.Group(Trace(), new FrameFilter(hideEngineFrames: true, null));

            Assert.That(groups.Select(g => g.IsNoise), Is.EqualTo(new[] { true, false, true }));
            Assert.That(groups[0].Count, Is.EqualTo(2), "the engine and capture frames fold together");
            Assert.That(groups[1].Frames.Select(f => f.TypeName), Is.EqualTo(new[] { "Game.Logging.Log", "Game.Boss.BossController" }));
            Assert.That(groups[2].Count, Is.EqualTo(1));
        }

        [Test]
        public void Group_FoldsConfiguredPrefixesToo()
        {
            var filter = new FrameFilter(hideEngineFrames: true, new[] { "Game.Logging." });

            var groups = FrameGrouper.Group(Trace(), filter);

            Assert.That(groups.Select(g => g.IsNoise), Is.EqualTo(new[] { true, false, true }));
            Assert.That(groups[0].Count, Is.EqualTo(3), "the project's logging wrapper folds with the engine frames");
            Assert.That(groups[1].Frames.Single().TypeName, Is.EqualTo("Game.Boss.BossController"));
        }

        [Test]
        public void Group_NeverFoldsTheEntryFrame()
        {
            ParsedTrace trace = Trace();
            var filter = new FrameFilter(hideEngineFrames: true, new[] { "Game." });

            var groups = FrameGrouper.Group(trace, filter);

            FrameGroup visible = groups.Single(g => !g.IsNoise);
            Assert.That(visible.Frames.Single(), Is.SameAs(trace.EntryFrame));
        }

        [Test]
        public void Group_WhenEverythingIsNoise_ShowsEverything()
        {
            ParsedTrace trace = StackTraceParser.Parse("UnityEngine.Debug:Log (object)\nUnityEngine.Logger:Log ()");

            var groups = FrameGrouper.Group(trace, new FrameFilter(hideEngineFrames: true, null));

            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].IsNoise, Is.False);
            Assert.That(groups[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void Group_EmptyTrace_YieldsNothing()
        {
            Assert.That(FrameGrouper.Group(ParsedTrace.Empty, new FrameFilter(true, null)), Is.Empty);
            Assert.That(FrameGrouper.Group(null, new FrameFilter(true, null)), Is.Empty);
        }

        [Test]
        public void Filter_ParsesPrefixesPerLine_IgnoringBlanksAndComments()
        {
            var prefixes = FrameFilter.ParsePrefixes("Cysharp.\n\n  # a comment\n  Game.Logging.  \n");

            Assert.That(prefixes, Is.EqualTo(new[] { "Cysharp.", "Game.Logging." }));
        }

        [Test]
        public void Filter_IsEmpty_WhenItWouldHideNothing()
        {
            Assert.That(new FrameFilter(false, null).IsEmpty, Is.True);
            Assert.That(new FrameFilter(false, new[] { "  ", null }).IsEmpty, Is.True);
            Assert.That(new FrameFilter(false, new[] { "Game." }).IsEmpty, Is.False);
            Assert.That(new FrameFilter(true, null).IsEmpty, Is.False);
        }
    }
}
