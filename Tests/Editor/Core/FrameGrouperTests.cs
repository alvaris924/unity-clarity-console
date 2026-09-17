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

        [TestCase("Library/PackageCache/com.unity.ai.assistant@284c/Modules/TraceSinks.cs", true)]
        [TestCase("./Library/PackageCache/com.unity.pipeline@1/Editor/Server.cs", true, TestName = "IsPackageFrame_AfterTheParserStripsTheDotSlash")]
        [TestCase("E:/Proj/Library/PackageCache/com.x@1/Runtime/X.cs", true)]
        [TestCase("Packages/com.mystudio.tools/Editor/Tool.cs", false, TestName = "IsPackageFrame_NotForAnEmbeddedPackage")]
        [TestCase("D:/Github/my-package/Editor/Capture.cs", false, TestName = "IsPackageFrame_NotForALocalFilePackage")]
        [TestCase("Assets/Game/Boss.cs", false)]
        [TestCase("", false)]
        public void IsPackageFrame_RecognisesInstalledPackageSources(string path, bool expected)
        {
            TraceFrame frame = StackTraceParser.Parse("Some.Type:Method () (at " + path + ":10)").Frames[0];

            Assert.That(frame.IsPackageFrame, Is.EqualTo(expected));
        }

        [Test]
        public void UnityNamespaces_CountAsEngineFrames()
        {
            Assert.That(StackTraceParser.Parse("Unity.AI.Tracing.ConsoleSink:LogToConsole (string) (at Library/PackageCache/x/T.cs:1)").Frames[0].IsEngineFrame, Is.True);
            Assert.That(StackTraceParser.Parse("UnityGame.Boss:Hit ()").Frames[0].IsEngineFrame, Is.False, "only the dotted Unity. prefix, not any type that starts with Unity");
        }

        [Test]
        public void Group_FoldsPackageFrames_OnlyWhenAsked_SoATraceMadeOfPackageCodeCollapsesToOneRow()
        {
            ParsedTrace trace = StackTraceParser.Parse(string.Join("\n", new[]
            {
                "UnityEngine.Debug:Log (object)",
                "Vendor.Logging.Sink:Write (string) (at Library/PackageCache/com.vendor.log@1.0/Runtime/Sink.cs:30)",
                "Vendor.Logging.Sink/<>c:<Write>b__0 () (at ./Library/PackageCache/com.vendor.log@1.0/Runtime/Sink.cs:12)",
                "UnityEditor.EditorApplication:Internal_CallUpdateFunctions ()",
            }));

            var folded = FrameGrouper.Group(trace, new FrameFilter(true, true, null));
            Assert.That(folded.Count, Is.EqualTo(1), "engine and package frames make one folded run");
            Assert.That(folded[0].IsNoise, Is.True);
            Assert.That(folded[0].Count, Is.EqualTo(4));
            Assert.That(FrameGrouper.FindEntryFrame(trace, new FrameFilter(true, true, null)), Is.Null, "nothing to preview or open");

            var kept = FrameGrouper.Group(trace, new FrameFilter(true, false, null));
            Assert.That(kept.Count, Is.EqualTo(3), "with the switch off the package frames stay visible between the engine runs");
            Assert.That(kept[1].IsNoise, Is.False);
            Assert.That(kept[1].Count, Is.EqualTo(2));
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
        public void Group_WhenEverythingIsNoise_FoldsItAllIntoOneRow()
        {
            ParsedTrace trace = StackTraceParser.Parse("UnityEngine.Debug:Log (object)\nUnityEngine.Logger:Log ()");

            var groups = FrameGrouper.Group(trace, new FrameFilter(hideEngineFrames: true, null));

            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].IsNoise, Is.True, "an entry logged from inside the engine shows no frames until asked");
            Assert.That(groups[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void FindEntryFrame_RescuesTheUsersOwnFrame_ButNotAnEngineOrPackageOne()
        {
            ParsedTrace user = Trace();
            Assert.That(FrameGrouper.FindEntryFrame(user, new FrameFilter(true, new[] { "Game." })), Is.SameAs(user.EntryFrame), "a prefix that folds the user's code still leaves their frame to open");

            ParsedTrace packageOnly = StackTraceParser.Parse(string.Join("\n", new[]
            {
                "UnityEngine.Debug:Log (object)",
                "ClarityConsole.Capture.LogHandlerWrapper:LogFormat () (at D:/pkg/Editor/Capture/LogHandlerWrapper.cs:27)",
                "Unity.AI.Tracing.ConsoleSink:LogToConsole (string) (at Library/PackageCache/com.unity.ai.assistant@1/TraceSinks.cs:313)",
            }));
            Assert.That(packageOnly.EntryFrame, Is.Not.Null, "unfiltered, something is located");
            Assert.That(FrameGrouper.FindEntryFrame(packageOnly, new FrameFilter(true, true, null)), Is.Null, "but none of it is the user's code");
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
            Assert.That(new FrameFilter(false, true, null).IsEmpty, Is.False, "folding package frames is a filter too");
            Assert.That(new FrameFilter(false, new[] { "Game." }).IsEmpty, Is.False);
            Assert.That(new FrameFilter(true, null).IsEmpty, Is.False);
        }
    }
}
