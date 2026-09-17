using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class CaptureFrameStripperTests
    {
        private const string LF = "\n";
        private const string CRLF = "\r\n";

        [Test]
        public void Strip_DropsTheHandlerAndWrapperFramesUnityAddsAtTheTopOfALog()
        {
            string trace =
                "UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])" + LF +
                "ClarityConsole.Capture.LogHandlerWrapper:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[]) (at D:/pkg/Editor/Capture/LogHandlerWrapper.cs:27)" + LF +
                "UnityEngine.Debug:Log (object)" + LF +
                "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)" + LF;

            Assert.That(CaptureFrameStripper.Strip(trace), Is.EqualTo(
                "UnityEngine.Debug:Log (object)" + LF +
                "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)" + LF));
        }

        [Test]
        public void Strip_DropsTheOverriddenHandlerFramesAtTheTailOfAnException()
        {
            string trace =
                "UnityEngine.UIElements.Panel.Repaint (UnityEngine.Event e) (at <8966e118d0054112ab94138b090bd323>:0)" + LF +
                "UnityEngine.GUIUtility.ProcessEvent (System.Int32 instanceID, System.IntPtr nativeEventPtr, System.Boolean& result) (at <d66e645ee8e84c079a0787ed6d025aea>:0)" + LF +
                "UnityEngine.DebugLogHandler:LogException(Exception, Object)" + LF +
                "ClarityConsole.Capture.LogHandlerWrapper:LogException(Exception, Object) (at D:/pkg/Editor/Capture/LogHandlerWrapper.cs:40)" + LF +
                "UnityEngine.Debug:CallOverridenDebugHandler(Exception, Object)" + LF;

            Assert.That(CaptureFrameStripper.Strip(trace), Is.EqualTo(
                "UnityEngine.UIElements.Panel.Repaint (UnityEngine.Event e) (at <8966e118d0054112ab94138b090bd323>:0)" + LF +
                "UnityEngine.GUIUtility.ProcessEvent (System.Int32 instanceID, System.IntPtr nativeEventPtr, System.Boolean& result) (at <d66e645ee8e84c079a0787ed6d025aea>:0)" + LF));
        }

        [Test]
        public void Strip_KeepsWindowsLineEndings_AndLeadingWhitespace()
        {
            string trace =
                "  ClarityConsole.Capture.LogHandlerWrapper:LogFormat ()" + CRLF +
                "  Game.Foo:Bar () (at Assets/Game/Foo.cs:20)" + CRLF;

            Assert.That(CaptureFrameStripper.Strip(trace), Is.EqualTo("  Game.Foo:Bar () (at Assets/Game/Foo.cs:20)" + CRLF));
        }

        [Test]
        public void Strip_ReturnsTheSameInstance_WhenThereIsNothingToDrop()
        {
            string trace = "UnityEngine.Debug:Log (object)" + LF + "Game.Foo:Bar () (at Assets/Game/Foo.cs:20)";

            Assert.That(CaptureFrameStripper.Strip(trace), Is.SameAs(trace));
            Assert.That(CaptureFrameStripper.Strip(string.Empty), Is.Empty);
            Assert.That(CaptureFrameStripper.Strip(null), Is.Null);
        }

        [Test]
        public void Strip_OnlyMatchesAtTheStartOfALine()
        {
            string trace = "Game.Logging:Write (string) (at Assets/Game/Logging.cs:9) // forwards to ClarityConsole.Capture.LogHandlerWrapper" + LF;

            Assert.That(CaptureFrameStripper.Strip(trace), Is.EqualTo(trace));
        }
    }
}
