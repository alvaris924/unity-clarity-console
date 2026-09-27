using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The path that led to an entry, drawn as a column of cards joined by arrows: the outermost call at
    /// the top, the line that logged or threw at the bottom. Each call card names the call, where it is,
    /// and the source around that line. Folded infrastructure runs are one small step that unfolds on
    /// click. Double-clicking a card opens its file in the code editor.
    /// </summary>
    internal sealed class CallFlowView : VisualElement
    {
        public const string CardClass = "cc-flow-card";
        public const string CardEntryClass = "cc-flow-card-entry";
        public const string CardOriginClass = "cc-flow-card-origin";
        public const string CardFailureClass = "cc-flow-card-failure";
        public const string CardLinkClass = "cc-flow-card-link";
        public const string FoldedClass = "cc-flow-folded";
        public const string ConnectorClass = "cc-flow-connector";
        public const string NoteClass = "cc-flow-note";

        private readonly Label _message;
        private readonly Label _summary;
        private readonly ScrollView _scroll;
        private readonly VisualElement _steps;
        private readonly HashSet<int> _expanded = new HashSet<int>();
        private List<FlowStep> _flow = new List<FlowStep>();
        private LogEntry _entry;
        private FrameFilter _frameFilter;

        public CallFlowView()
        {
            AddToClassList("cc-flow");

            var header = new VisualElement();
            header.AddToClassList("cc-flow-header");
            _message = new Label();
            _message.AddToClassList("cc-flow-message");
            _message.AddToClassList("cc-mono");
            header.Add(_message);
            _summary = new Label();
            _summary.AddToClassList("cc-flow-summary");
            header.Add(_summary);
            Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("cc-flow-scroll");
            _steps = new VisualElement();
            _steps.AddToClassList("cc-flow-steps");
            _scroll.Add(_steps);
            Add(_scroll);
        }

        /// <summary>Raised when a frame should be opened in the code editor.</summary>
        public event Action<TraceFrame> FrameActivated;

        /// <summary>Asks for the source around a frame. Returning null draws the card without source.</summary>
        public Func<TraceFrame, SourceSnippet> SnippetProvider { get; set; }

        /// <summary>Which frames count as infrastructure and fold into one step. Null folds nothing.</summary>
        public FrameFilter FrameFilter
        {
            get => _frameFilter;
            set
            {
                _frameFilter = value;
                if (_entry != null)
                {
                    Show(_entry);
                }
            }
        }

        /// <summary>The entry on display, or null.</summary>
        public LogEntry Entry => _entry;

        /// <summary>The steps currently drawn, outermost first.</summary>
        public IReadOnlyList<FlowStep> Steps => _flow;

        /// <summary>The message line at the top, for tests.</summary>
        internal string MessageText => _message.text;

        /// <summary>The line under the message that counts the steps, for tests.</summary>
        internal string SummaryText => _summary.text;

        public void Show(LogEntry entry)
        {
            if (!ReferenceEquals(entry, _entry))
            {
                _expanded.Clear();
            }

            _entry = entry;
            Render();
        }

        /// <summary>Unfolds a folded run so each of its frames becomes a card.</summary>
        public void ExpandGroup(int groupIndex)
        {
            if (_expanded.Add(groupIndex))
            {
                Render();
            }
        }

        /// <summary>The first line of a message, which is all the header has room for.</summary>
        internal static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            int end = message.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? message : message.Substring(0, end);
        }

        /// <summary>"5 calls, 3 frames folded" and the like.</summary>
        internal static string Summarise(IReadOnlyList<FlowStep> steps)
        {
            int calls = 0;
            int folded = 0;
            foreach (FlowStep step in steps)
            {
                if (step.Kind == FlowStepKind.Call)
                {
                    calls++;
                }
                else
                {
                    folded += step.Frames.Count;
                }
            }

            string text = calls + (calls == 1 ? " call" : " calls");
            if (folded > 0)
            {
                text += ", " + folded + (folded == 1 ? " frame folded" : " frames folded");
            }

            return text + ", read from the first call down to where it was logged";
        }

        /// <summary>The small tag beside a call's name, or empty for none.</summary>
        internal static string NoteText(FlowStep step, LogSeverity severity)
        {
            if (step.IsOrigin)
            {
                return severity == LogSeverity.Exception ? "threw here" : "logged here";
            }

            switch (step.Name.Note)
            {
                case CallNote.Resumed:
                    return "resumed";
                case CallNote.Lambda:
                    return "lambda";
                case CallNote.LocalFunction:
                    return "local function";
                default:
                    return string.Empty;
            }
        }

        private void Render()
        {
            _steps.Clear();

            if (_entry == null)
            {
                _flow = new List<FlowStep>();
                _message.text = string.Empty;
                _summary.text = "Select an entry in the console and choose Show flow.";
                return;
            }

            _flow = CallFlow.Build(_entry, _frameFilter, _expanded);
            _message.text = FirstLine(_entry.Message);
            _message.EnableInClassList("cc-msg-warning", _entry.Severity == LogSeverity.Warning);
            _message.EnableInClassList("cc-msg-error", _entry.Severity >= LogSeverity.Error);
            _summary.text = _flow.Count == 0 ? "This entry has no stack trace to follow." : Summarise(_flow);

            VisualElement focus = null;
            int number = 0;
            for (int i = 0; i < _flow.Count; i++)
            {
                if (i > 0)
                {
                    _steps.Add(new FlowConnector());
                }

                FlowStep step = _flow[i];
                VisualElement element;
                if (step.Kind == FlowStepKind.Folded)
                {
                    element = BuildFolded(step);
                }
                else
                {
                    number++;
                    element = BuildCard(step, number);
                }

                _steps.Add(element);
                if (step.IsEntry)
                {
                    focus = element;
                }
            }

            // Land on the reader's own code; the scroll view needs a layout pass before it can scroll to it.
            if (focus != null)
            {
                VisualElement target = focus;
                _scroll.schedule.Execute(() =>
                {
                    if (target.panel != null)
                    {
                        _scroll.ScrollTo(target);
                    }
                });
            }
        }

        private VisualElement BuildCard(FlowStep step, int number)
        {
            TraceFrame frame = step.Frame;
            var card = new VisualElement();
            card.AddToClassList(CardClass);
            card.EnableInClassList(CardEntryClass, step.IsEntry);
            card.EnableInClassList(CardOriginClass, step.IsOrigin);
            card.EnableInClassList(CardFailureClass, step.IsOrigin && _entry.Severity >= LogSeverity.Error);

            var title = new VisualElement();
            title.AddToClassList("cc-flow-title");

            var index = new Label(number.ToString());
            index.AddToClassList("cc-flow-index");
            title.Add(index);

            var name = new Label(step.Name.ToString());
            name.AddToClassList("cc-flow-name");
            name.AddToClassList("cc-mono");
            name.tooltip = frame.Signature;
            title.Add(name);

            string note = NoteText(step, _entry.Severity);
            if (note.Length > 0)
            {
                var badge = new Label(note);
                badge.AddToClassList(NoteClass);
                title.Add(badge);
            }

            card.Add(title);

            var location = new Label(frame.HasLocation ? frame.FilePath + ":" + frame.Line : "no source location");
            location.AddToClassList("cc-flow-location");
            card.Add(location);

            if (frame.HasLocation)
            {
                SourceSnippet snippet = SnippetProvider?.Invoke(frame);
                if (snippet != null)
                {
                    var block = new SourcePreview { Inline = true };
                    block.AddToClassList("cc-flow-source");
                    block.Show(snippet, frame.FilePath);
                    card.Add(block);
                }

                card.AddToClassList(CardLinkClass);
                card.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount >= 2)
                    {
                        FrameActivated?.Invoke(frame);
                    }
                });
            }

            return card;
        }

        private Label BuildFolded(FlowStep step)
        {
            var group = new FrameGroup(true, step.Frames);
            var label = new Label(DetailView.SummariseHidden(group));
            label.AddToClassList(FoldedClass);
            label.tooltip = "Infrastructure frames. Click to show each call.";
            int groupIndex = step.GroupIndex;
            label.RegisterCallback<ClickEvent>(_ => ExpandGroup(groupIndex));
            return label;
        }
    }

    /// <summary>The arrow between two steps, drawn in the theme's dimmed text colour.</summary>
    internal sealed class FlowConnector : VisualElement
    {
        private static readonly CustomStyleProperty<Color> LineProperty = new CustomStyleProperty<Color>("--cc-flow-line");

        private Color _color = new Color(0.55f, 0.55f, 0.55f, 0.9f);

        public FlowConnector()
        {
            AddToClassList(CallFlowView.ConnectorClass);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if (evt.customStyle.TryGetValue(LineProperty, out Color color))
                {
                    _color = color;
                    MarkDirtyRepaint();
                }
            });
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            float width = contentRect.width;
            float height = contentRect.height;
            if (width <= 0 || height <= 6)
            {
                return;
            }

            const float Head = 5f;
            float x = Mathf.Round(Math.Min(width, 28f) / 2f);
            Painter2D painter = context.painter2D;

            painter.strokeColor = _color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, 1f));
            painter.LineTo(new Vector2(x, height - Head));
            painter.Stroke();

            painter.fillColor = _color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x - Head, height - Head - 1f));
            painter.LineTo(new Vector2(x + Head, height - Head - 1f));
            painter.LineTo(new Vector2(x, height - 1f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
