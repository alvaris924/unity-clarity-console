using System;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class LogFileParserTests
    {
        private static string Join(params string[] lines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        [Test]
        public void Logcat_IsDetected_AndCarriesLevelTagAndTime()
        {
            LogFileImport import = LogFileParser.Parse(Join(
                "09-16 07:19:20.123  1234  5678 I Unity   : Loading scene Main",
                "09-16 07:19:20.456  1234  5678 W Unity   : Texture memory is high",
                "09-16 07:19:21.000  1234  5678 E Unity   : NullReferenceException"));

            Assert.That(import.Format, Is.EqualTo(LogFileFormat.Logcat));
            Assert.That(import.Describe(), Is.EqualTo("Android logcat"));
            Assert.That(import.Entries.Count, Is.EqualTo(3));
            Assert.That(import.Entries[0].Message, Is.EqualTo("[Unity] Loading scene Main"));
            Assert.That(import.Entries.Select(e => e.Severity), Is.EqualTo(new[] { LogSeverity.Log, LogSeverity.Warning, LogSeverity.Error }));
            // logcat stamps are local to the device, so compare in local time rather than UTC.
            DateTime local = import.Entries[0].TimestampUtc.ToLocalTime();
            Assert.That(local.Month, Is.EqualTo(9));
            Assert.That(local.Day, Is.EqualTo(16));
            Assert.That(local.ToString("HH:mm:ss.fff"), Is.EqualTo("07:19:20.123"));
        }

        [Test]
        public void Logcat_ContinuationLines_JoinTheEntryAbove()
        {
            LogFileImport import = LogFileParser.Parse(Join(
                "09-16 07:19:21.000  1234  5678 E Unity   : NullReferenceException",
                "  at Game.Boss.Update () [0x00000] in Assets/Boss.cs:88",
                "09-16 07:19:22.000  1234  5678 I Unity   : recovered"));

            Assert.That(import.Entries.Count, Is.EqualTo(2));
            Assert.That(import.Entries[0].StackTrace, Does.Contain("Assets/Boss.cs:88"));
            Assert.That(import.Entries[1].Message, Is.EqualTo("[Unity] recovered"));
        }

        [Test]
        public void UnityLog_SplitsOnBlankLines_AndKeepsFramesAsTheStack()
        {
            LogFileImport import = LogFileParser.Parse(Join(
                "Loading scene Main",
                string.Empty,
                "NullReferenceException: Object reference not set to an instance of an object",
                "  at Game.Boss.Update () [0x00000] in Assets/Boss.cs:88",
                "  at UnityEngine.MonoBehaviour.Invoke () [0x00000] in <unknown>:0",
                string.Empty));

            Assert.That(import.Format, Is.EqualTo(LogFileFormat.UnityLog));
            Assert.That(import.Entries.Count, Is.EqualTo(2));
            Assert.That(import.Entries[1].Severity, Is.EqualTo(LogSeverity.Exception));
            Assert.That(import.Entries[1].Message, Does.StartWith("NullReferenceException:"));
            Assert.That(import.Entries[1].StackTrace, Does.Contain("Assets/Boss.cs:88"));
            Assert.That(import.Entries[1].Trace.EntryFrame, Is.Not.Null);
        }

        [Test]
        public void UnityLog_FilenameSuffix_BecomesAClickableFrame()
        {
            LogFileImport import = LogFileParser.Parse(Join(
                "Something went wrong",
                "(Filename: Assets/Game/Foo.cs Line: 42)"));

            LogEntry entry = import.Entries.Single();
            Assert.That(entry.StackTrace.Trim(), Is.EqualTo("(at Assets/Game/Foo.cs:42)"));
            Assert.That(entry.Trace.EntryFrame, Is.Not.Null, "the call site must be clickable after an import");
            Assert.That(entry.Trace.EntryFrame.FilePath, Is.EqualTo("Assets/Game/Foo.cs"));
            Assert.That(entry.Trace.EntryFrame.Line, Is.EqualTo(42));
        }

        [Test]
        public void UnityLog_MultiLineMessage_StaysOneMessageUntilAFrameAppears()
        {
            LogEntry entry = LogFileParser.Parse(Join(
                "First line of the message",
                "second line, still the message",
                "  at Game.Foo.Bar () [0x00000] in Assets/Foo.cs:1")).Entries.Single();

            Assert.That(entry.Message, Is.EqualTo("First line of the message" + Environment.NewLine.Replace("\r\n", "\n") + "second line, still the message").Or.EqualTo("First line of the message\nsecond line, still the message"));
            Assert.That(entry.StackTrace, Does.Contain("Assets/Foo.cs:1"));
        }

        [TestCase("Warning: shader is slow", LogSeverity.Warning)]
        [TestCase("Error: could not load bundle", LogSeverity.Error)]
        [TestCase("Assertion failed on expression", LogSeverity.Assert)]
        [TestCase("InvalidOperationException: bad state", LogSeverity.Exception)]
        [TestCase("Assets/Foo.cs(12,3): error CS0103: nope", LogSeverity.Error)]
        [TestCase("A perfectly ordinary line", LogSeverity.Log)]
        [TestCase("The error was handled gracefully", LogSeverity.Log)]
        public void UnityLog_SeverityComesFromMarkersUnityWrites_NotGuesswork(string line, LogSeverity expected)
        {
            Assert.That(LogFileParser.SeverityFromUnityLine(line), Is.EqualTo(expected));
        }

        [Test]
        public void Entries_AreNumberedInFileOrder()
        {
            LogFileImport import = LogFileParser.Parse(Join("one", string.Empty, "two", string.Empty, "three"));

            Assert.That(import.Entries.Select(e => e.Id), Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        [TestCase("")]
        [TestCase("   \n  \n")]
        [TestCase(null)]
        public void EmptyInput_ImportsNothing(string text)
        {
            LogFileImport import = LogFileParser.Parse(text);

            Assert.That(import.Entries, Is.Empty);
            Assert.That(import.Format, Is.EqualTo(LogFileFormat.Unknown));
            Assert.That(import.Describe(), Is.EqualTo("Plain text"));
        }

        [Test]
        public void Detect_PrefersLogcatWhenBothCouldMatch()
        {
            var lines = new[] { "09-16 07:19:20.123  1 2 I Unity   : at Assets/Foo.cs:1" };

            Assert.That(LogFileParser.Detect(lines), Is.EqualTo(LogFileFormat.Logcat));
        }
    }
}
