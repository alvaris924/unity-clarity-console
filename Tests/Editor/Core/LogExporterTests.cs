using System;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class LogExporterTests
    {
        private static readonly DateTime When = new DateTime(2026, 9, 15, 10, 30, 0, DateTimeKind.Utc);

        [Test]
        public void Text_WritesHeaderThenOneBlockPerEntry()
        {
            string text = LogExporter.Export(
                new[] { Entry(LogSeverity.Error, "[Net] boom", "Game.Foo:Bar () (at Assets/Foo.cs:12)") },
                new ExportOptions { Header = new[] { "Unity 6000.2.9f1", "Session 3" } });

            Assert.That(text, Does.StartWith("# Unity 6000.2.9f1" + Environment.NewLine + "# Session 3"));
            Assert.That(text, Does.Contain("] ERROR [Net] boom"), "the channel is only repeated when the store assigned one");
            Assert.That(text, Does.Contain("    Game.Foo:Bar () (at Assets/Foo.cs:12)"));
        }

        [Test]
        public void Text_WithoutStackTraces_StaysShort()
        {
            string text = LogExporter.Export(
                new[] { Entry(LogSeverity.Log, "hello", "Game.Foo:Bar () (at Assets/Foo.cs:12)") },
                new ExportOptions { IncludeStackTraces = false });

            Assert.That(text, Does.Contain("hello"));
            Assert.That(text, Does.Not.Contain("Assets/Foo.cs"));
        }

        [Test]
        public void Text_LabelsMarkers()
        {
            string text = LogExporter.Export(new[] { LogEntry.Marker("Domain reloaded", When, 0) }, null);

            Assert.That(text, Does.Contain("MARKER Domain reloaded"));
        }

        [Test]
        public void Markdown_UsesHeadingsAndFencesTheStack()
        {
            string text = LogExporter.Export(
                new[] { Entry(LogSeverity.Warning, "careful", "Game.Foo:Bar ()") },
                new ExportOptions { Format = ExportFormat.Markdown, Header = new[] { "Session 3" } });

            Assert.That(text, Does.StartWith("# Console export"));
            Assert.That(text, Does.Contain("- Session 3"));
            Assert.That(text, Does.Contain("### WARNING careful"));
            Assert.That(text, Does.Contain("```"));
            Assert.That(text, Does.Contain("Game.Foo:Bar ()"));
        }

        [Test]
        public void Json_EscapesAndCarriesTheFields()
        {
            LogEntry entry = Entry(LogSeverity.Exception, "quote \" and \\ and\nnewline", "Game.Foo:Bar ()");
            entry.AssignSequence(7, 2);
            entry.Channel = "Net";

            string json = LogExporter.Export(new[] { entry }, new ExportOptions { Format = ExportFormat.Json, Header = new[] { "Session 2" } });

            Assert.That(json, Does.StartWith("{"));
            Assert.That(json, Does.EndWith("}"));
            Assert.That(json, Does.Contain("\"header\": [\"Session 2\"],"));
            Assert.That(json, Does.Contain("\"id\": 7"));
            Assert.That(json, Does.Contain("\"severity\": \"Exception\""));
            Assert.That(json, Does.Contain("\"channel\": \"Net\""));
            Assert.That(json, Does.Contain("\"message\": \"quote \\\" and \\\\ and\\nnewline\""));
            Assert.That(json, Does.Contain("\"time\": \"2026-09-15T10:30:00.0000000Z\""));
        }

        [Test]
        public void Json_WithSeveralEntries_SeparatesThemWithCommas()
        {
            string json = LogExporter.Export(
                new[] { Entry(LogSeverity.Log, "one", string.Empty), Entry(LogSeverity.Log, "two", string.Empty) },
                new ExportOptions { Format = ExportFormat.Json });

            Assert.That(json.Split(new[] { "\"message\"" }, StringSplitOptions.None).Length - 1, Is.EqualTo(2));
            Assert.That(json, Does.Contain("},"));
        }

        [Test]
        public void Export_WithNothingToWrite_StillProducesValidOutput()
        {
            Assert.That(LogExporter.Export(null, null), Is.Empty);
            Assert.That(LogExporter.Export(Array.Empty<LogEntry>(), new ExportOptions { Format = ExportFormat.Json }),
                Is.EqualTo("{" + Environment.NewLine + "  \"entries\": [" + Environment.NewLine + "  ]" + Environment.NewLine + "}"));
        }

        [TestCase(ExportFormat.Text, "txt")]
        [TestCase(ExportFormat.Markdown, "md")]
        [TestCase(ExportFormat.Json, "json")]
        public void ExtensionFor_MatchesTheFormat(ExportFormat format, string expected)
        {
            Assert.That(LogExporter.ExtensionFor(format), Is.EqualTo(expected));
        }

        [Test]
        public void Quote_EscapesControlCharacters()
        {
            Assert.That(LogExporter.Quote("tab\there"), Is.EqualTo("\"tab\\there\""));
            Assert.That(LogExporter.Quote("bell" + (char)7), Is.EqualTo("\"bell\\u0007\""));
            Assert.That(LogExporter.Quote(null), Is.EqualTo("\"\""));
        }

        private static LogEntry Entry(LogSeverity severity, string message, string stackTrace)
        {
            return new LogEntry(LogEntryKind.Log, severity, message, stackTrace, When, 12, 1, true, ObjectRef.None);
        }
    }
}
