using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class StackTraceParserTests
    {
        [Test]
        public void Parse_EditorLogFormat_ExtractsMethodAndLocation()
        {
            ParsedTrace trace = StackTraceParser.Parse(
                "UnityEngine.Debug:Log (object)\n" +
                "PlayFabCBSManager:FetchInventoryAsync () (at Assets/0_Scripts/PlayFabCBS/PlayFabCBSManager.cs:214)\n");

            Assert.That(trace.Frames.Count, Is.EqualTo(2));

            TraceFrame engine = trace.Frames[0];
            Assert.That(engine.HasLocation, Is.False);
            Assert.That(engine.TypeName, Is.EqualTo("UnityEngine.Debug"));
            Assert.That(engine.MethodName, Is.EqualTo("Log"));
            Assert.That(engine.IsEngineFrame, Is.True);

            TraceFrame user = trace.Frames[1];
            Assert.That(user.HasLocation, Is.True);
            Assert.That(user.FilePath, Is.EqualTo("Assets/0_Scripts/PlayFabCBS/PlayFabCBSManager.cs"));
            Assert.That(user.Line, Is.EqualTo(214));
            Assert.That(user.TypeName, Is.EqualTo("PlayFabCBSManager"));
            Assert.That(user.MethodName, Is.EqualTo("FetchInventoryAsync"));
            Assert.That(user.Signature, Is.EqualTo("PlayFabCBSManager:FetchInventoryAsync ()"));
        }

        [Test]
        public void Parse_FilePackageFormat_AbsolutePathFromRealEditorOutput()
        {
            // Observed on Unity 6000.2 for a package installed from a file: path.
            ParsedTrace trace = StackTraceParser.Parse(
                "ClarityConsole.Capture.LogHandlerWrapper:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[]) (at D:/Github/UnityClarityConsole/unity-clarity-console/Editor/Capture/LogHandlerWrapper.cs:27)\n" +
                "Game.Tests.SmokeTests:Log_StackTrace_ParsesToAFrameInThisTestFile () (at D:/Github/UnityClarityConsole/unity-clarity-console/Tests/Editor/Capture/LogCaptureTests.cs:157)\n");

            Assert.That(trace.Frames[0].IsEngineFrame, Is.True, "the package's own capture frame is never the entry frame");
            Assert.That(trace.Frames[0].FilePath, Is.EqualTo("D:/Github/UnityClarityConsole/unity-clarity-console/Editor/Capture/LogHandlerWrapper.cs"));
            Assert.That(trace.EntryFrame, Is.SameAs(trace.Frames[1]));
            Assert.That(trace.EntryFrame.Line, Is.EqualTo(157));
        }

        [Test]
        public void Parse_ExceptionFormat_UsesDotSeparator()
        {
            ParsedTrace trace = StackTraceParser.Parse("Game.Boss.BossController.TakeDamage (System.Int32 amount) (at Assets/Game/BossController.cs:88)");

            TraceFrame frame = trace.Frames.Single();
            Assert.That(frame.TypeName, Is.EqualTo("Game.Boss.BossController"));
            Assert.That(frame.MethodName, Is.EqualTo("TakeDamage"));
            Assert.That(frame.Line, Is.EqualTo(88));
        }

        [Test]
        public void Parse_MonoFormat_WithOffsetAndBackslashes_ExtractsLocation()
        {
            ParsedTrace trace = StackTraceParser.Parse(@"  at Foo.Bar (System.Int32 index) [0x00001] in D:\proj\Assets\Foo.cs:12 ");

            TraceFrame frame = trace.Frames.Single();
            Assert.That(frame.HasLocation, Is.True);
            Assert.That(frame.FilePath, Is.EqualTo("D:/proj/Assets/Foo.cs"));
            Assert.That(frame.Line, Is.EqualTo(12));
            Assert.That(frame.TypeName, Is.EqualTo("Foo"));
            Assert.That(frame.MethodName, Is.EqualTo("Bar"));
        }

        [TestCase("Foo:Bar () (at <UnknownFile>:0)")]
        [TestCase("at Foo.Bar () [0x00000] in <a1b2c3d4>:0")]
        [TestCase("Foo.Bar () (at <filename unknown>:0)")]
        public void Parse_UnknownLocation_YieldsFrameWithoutLocation(string line)
        {
            TraceFrame frame = StackTraceParser.Parse(line).Frames.Single();

            Assert.That(frame.HasLocation, Is.False);
            Assert.That(frame.MethodName, Is.EqualTo("Bar"));
            Assert.That(frame.Raw, Is.EqualTo(line));
        }

        [Test]
        public void Parse_SkipsBlankLines_AndAcceptsCrlf()
        {
            ParsedTrace trace = StackTraceParser.Parse("A:B () (at Assets/A.cs:1)\r\n\r\n  \r\nC:D () (at Assets/C.cs:2)\r\n");

            Assert.That(trace.Frames.Select(f => f.Line), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Parse_EmptyOrNull_ReturnsEmptyTrace()
        {
            Assert.That(StackTraceParser.Parse(null).Frames, Is.Empty);
            Assert.That(StackTraceParser.Parse("  \n").Frames, Is.Empty);
            Assert.That(StackTraceParser.Parse(null).EntryFrame, Is.Null);
        }

        [Test]
        public void SplitSignature_IgnoresSeparatorsInsideGenericArguments()
        {
            StackTraceParser.SplitSignature("Game.Pool`1[System.Collections.Generic.List`1[System.Int32]]:Rent ()", out string type, out string method);

            Assert.That(type, Is.EqualTo("Game.Pool`1[System.Collections.Generic.List`1[System.Int32]]"));
            Assert.That(method, Is.EqualTo("Rent"));
        }

        [Test]
        public void SplitSignature_NestedAsyncStateMachine_KeepsOuterTypeName()
        {
            StackTraceParser.SplitSignature("PlayFabCBSManager/<RunPhase2Async>d__12:MoveNext ()", out string type, out string method);

            Assert.That(type, Is.EqualTo("PlayFabCBSManager/<RunPhase2Async>d__12"));
            Assert.That(method, Is.EqualTo("MoveNext"));
        }

        [Test]
        public void EntryFrame_PrefersAssetsOverPackageCacheAndEngineFrames()
        {
            ParsedTrace trace = StackTraceParser.Parse(
                "UnityEngine.Debug:Log (object)\n" +
                "UnityEngine.Logger:Log (object) (at Library/PackageCache/com.unity.foo@1.0.0/Runtime/Logger.cs:10)\n" +
                "Cysharp.Threading.Tasks.UniTask:Run () (at Library/PackageCache/com.cysharp.unitask@2.5.0/Runtime/UniTask.cs:30)\n" +
                "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)\n" +
                "Game.Foo:Baz () (at Assets/Game/Foo.cs:40)\n");

            Assert.That(trace.EntryFrame.Line, Is.EqualTo(20));
        }

        [Test]
        public void EntryFrame_FallsBackToNonEngineFrameOutsidePackageCache_ThenToAnyLocatedFrame()
        {
            ParsedTrace outsideCache = StackTraceParser.Parse(
                "UnityEngine.Debug:Log (object) (at Library/PackageCache/com.unity.x@1/Debug.cs:1)\n" +
                "Cysharp.Threading.Tasks.UniTask:Run () (at Library/PackageCache/com.cysharp.unitask@2.5.0/Runtime/UniTask.cs:30)\n" +
                "MyCompany.Tools.Logger:Write () (at Packages/com.mycompany.tools/Runtime/Logger.cs:120)\n");
            Assert.That(outsideCache.EntryFrame.Line, Is.EqualTo(120));

            ParsedTrace onlyCache = StackTraceParser.Parse(
                "UnityEngine.Debug:Log (object)\n" +
                "Cysharp.Threading.Tasks.UniTask:Run () (at Library/PackageCache/com.cysharp.unitask@2.5.0/Runtime/UniTask.cs:30)\n");
            Assert.That(onlyCache.EntryFrame.Line, Is.EqualTo(30));
        }
    }
}
