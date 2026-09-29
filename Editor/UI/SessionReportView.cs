using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// Draws a <see cref="SessionReport"/>: a verdict line, the run's length and frame rate, counts by
    /// severity, the first error with a way to it, and the messages and channels that logged the most.
    /// Clicking a line asks for its first entry to be shown in the console.
    /// </summary>
    internal sealed class SessionReportView : VisualElement
    {
        public const string VerdictClass = "cc-report-verdict";
        public const string TileClass = "cc-report-tile";
        public const string LineClass = "cc-report-line";
        public const string SectionClass = "cc-report-section";

        private readonly ScrollView _scroll;
        private SessionReport _report;

        public SessionReportView()
        {
            AddToClassList("cc-report");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("cc-report-scroll");
            Add(_scroll);
        }

        /// <summary>Raised with an entry the reader wants to see in the console.</summary>
        public event Action<LogEntry> EntryRequested;

        /// <summary>Raised with the first error when the reader wants its call path.</summary>
        public event Action<LogEntry> FlowRequested;

        public SessionReport Report => _report;

        public void Show(SessionReport report)
        {
            _report = report;
            VisualElement page = _scroll.contentContainer;
            page.Clear();

            if (report == null)
            {
                page.Add(Text("No Play session yet. Enter Play mode, then open the report again.", "cc-report-note"));
                return;
            }

            page.Add(Text("Session " + report.Session, "cc-report-title"));

            Label verdict = Text(report.Verdict, VerdictClass);
            verdict.EnableInClassList("cc-msg-error", report.Errors > 0);
            verdict.EnableInClassList("cc-msg-warning", report.Errors == 0 && report.Warnings > 0);
            page.Add(verdict);

            if (!report.HasEntries)
            {
                return;
            }

            page.Add(Text(Describe(report), "cc-report-note"));

            var tiles = new VisualElement();
            tiles.AddToClassList("cc-report-tiles");
            tiles.Add(Tile(report.Logs, "logs", null));
            tiles.Add(Tile(report.Warnings, report.Warnings == 1 ? "warning" : "warnings", "cc-msg-warning"));
            tiles.Add(Tile(report.Errors, report.Errors == 1 ? "error" : "errors", "cc-msg-error"));
            page.Add(tiles);

            if (report.FirstError != null)
            {
                page.Add(Section("First error"));
                page.Add(FirstErrorCard(report));
            }

            AddLines(page, "Most repeated errors", report.TopErrors, report.Errors > 1);
            AddLines(page, "Chattiest messages", report.TopMessages, true);
            AddLines(page, "Busiest channels", report.TopChannels, true);
        }

        /// <summary>"Ran 3:12, about 58 fps. Still running." and the like.</summary>
        internal static string Describe(SessionReport report)
        {
            string text = "Ran " + SessionReport.FormatOffset(report.Duration);
            if (report.FramesPerSecond > 0)
            {
                text += ", about " + Math.Round(report.FramesPerSecond).ToString("N0") + " fps over " + report.FramesSpanned.ToString("N0") + " frames";
            }

            text += ".";
            if (!report.HasEnded)
            {
                text += " Still running, or the Editor stopped before the run ended.";
            }

            if (report.IsTruncated)
            {
                text += " The start of the run has scrolled out of the console, so these numbers cover only what is left.";
            }

            return text;
        }

        private VisualElement FirstErrorCard(SessionReport report)
        {
            LogEntry error = report.FirstError;
            var card = new VisualElement();
            card.AddToClassList("cc-report-card");

            Label message = Text(CallFlowView.FirstLine(error.Message), "cc-report-error");
            message.AddToClassList("cc-mono");
            message.AddToClassList("cc-msg-error");
            card.Add(message);

            TraceFrame frame = error.Trace.EntryFrame;
            string where = SessionReport.FormatOffset(error.TimestampUtc - report.StartUtc) + " into the run";
            if (frame != null)
            {
                where += ", " + frame.FilePath + ":" + frame.Line;
            }

            card.Add(Text(where, "cc-report-note"));

            var buttons = new VisualElement();
            buttons.AddToClassList("cc-report-buttons");
            var show = new Button(() => EntryRequested?.Invoke(error)) { text = "Show in console", name = "show-first-error" };
            buttons.Add(show);
            if (CallFlow.CanShow(error))
            {
                var flow = new Button(() => FlowRequested?.Invoke(error)) { text = "Show flow", name = "flow-first-error" };
                buttons.Add(flow);
            }

            card.Add(buttons);
            return card;
        }

        private void AddLines(VisualElement page, string title, IReadOnlyList<ReportLine> lines, bool worthShowing)
        {
            if (!worthShowing || lines.Count == 0)
            {
                return;
            }

            page.Add(Section(title));
            foreach (ReportLine line in lines)
            {
                ReportLine chosen = line;
                var row = new VisualElement();
                row.AddToClassList(LineClass);
                row.Add(Text("×" + line.Count.ToString("N0"), "cc-report-count"));
                Label text = Text(CallFlowView.FirstLine(line.Text), "cc-report-text");
                text.AddToClassList("cc-mono");
                row.Add(text);
                row.tooltip = "Click to show the first one in the console.";
                row.RegisterCallback<ClickEvent>(_ => EntryRequested?.Invoke(chosen.First));
                page.Add(row);
            }
        }

        private static VisualElement Tile(int count, string label, string severityClass)
        {
            var tile = new VisualElement();
            tile.AddToClassList(TileClass);
            Label number = Text(count.ToString("N0"), "cc-report-tile-count");
            if (severityClass != null && count > 0)
            {
                number.AddToClassList(severityClass);
            }

            tile.Add(number);
            tile.Add(Text(label, "cc-report-tile-label"));
            return tile;
        }

        private static Label Section(string title)
        {
            Label label = Text(title, SectionClass);
            return label;
        }

        private static Label Text(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }
    }
}
