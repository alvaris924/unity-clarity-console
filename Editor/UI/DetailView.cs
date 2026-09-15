using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The pane under the list: the selected entry's message, one row per stack frame, and a preview of
    /// the source around the selected frame. Clicking a frame selects it and moves the preview;
    /// double-clicking it, or the preview, opens the file in the code editor.
    /// </summary>
    internal sealed class DetailView : ScrollView
    {
        public const string FrameClass = "cc-frame";
        public const string FrameLinkClass = "cc-frame-link";
        public const string FrameEntryClass = "cc-frame-entry";
        public const string FrameSelectedClass = "cc-frame-selected";

        private readonly TextField _message;
        private readonly VisualElement _frames;
        private readonly SourcePreview _preview;
        private readonly Dictionary<TraceFrame, Label> _rows = new Dictionary<TraceFrame, Label>();

        public DetailView()
            : base(ScrollViewMode.Vertical)
        {
            AddToClassList("cc-detail");

            _message = new TextField { multiline = true, isReadOnly = true };
            _message.AddToClassList("cc-detail-message");
            Add(_message);

            _frames = new VisualElement();
            _frames.AddToClassList("cc-frames");
            Add(_frames);

            _preview = new SourcePreview();
            _preview.Activated += () => OpenSelectedFrame();
            Add(_preview);
        }

        /// <summary>Raised when a frame should be opened in the code editor.</summary>
        public event Action<TraceFrame> FrameActivated;

        /// <summary>Asks for the source around a frame. Returning null hides the preview.</summary>
        public Func<TraceFrame, SourceSnippet> SnippetProvider { get; set; }

        /// <summary>The frame the preview is showing, or null.</summary>
        public TraceFrame SelectedFrame { get; private set; }

        public int FrameRowCount => _frames.childCount;

        public bool IsPreviewVisible => _preview.style.display.value == DisplayStyle.Flex;

        public void Show(LogEntry entry)
        {
            _frames.Clear();
            _rows.Clear();
            SelectedFrame = null;
            _preview.Hide();

            if (entry == null)
            {
                _message.SetValueWithoutNotify(string.Empty);
                return;
            }

            _message.SetValueWithoutNotify(entry.Message);

            ParsedTrace trace = entry.Trace;
            foreach (TraceFrame frame in trace.Frames)
            {
                Label row = BuildRow(frame, ReferenceEquals(frame, trace.EntryFrame));
                _rows[frame] = row;
                _frames.Add(row);
            }

            SelectFrame(trace.EntryFrame);
        }

        /// <summary>Moves the preview to a frame. A frame without a location clears it.</summary>
        public void SelectFrame(TraceFrame frame)
        {
            if (SelectedFrame != null && _rows.TryGetValue(SelectedFrame, out Label previous))
            {
                previous.RemoveFromClassList(FrameSelectedClass);
            }

            SelectedFrame = frame;
            if (frame == null)
            {
                _preview.Hide();
                return;
            }

            if (_rows.TryGetValue(frame, out Label row))
            {
                row.AddToClassList(FrameSelectedClass);
            }

            SourceSnippet snippet = frame.HasLocation && SnippetProvider != null ? SnippetProvider(frame) : null;
            _preview.Show(snippet, snippet == null ? null : frame.FilePath);
        }

        internal static string Format(TraceFrame frame)
        {
            return frame.HasLocation ? frame.Signature + "    " + frame.FilePath + ":" + frame.Line : frame.Signature;
        }

        private void OpenSelectedFrame()
        {
            if (SelectedFrame != null && SelectedFrame.HasLocation)
            {
                FrameActivated?.Invoke(SelectedFrame);
            }
        }

        private Label BuildRow(TraceFrame frame, bool isEntry)
        {
            var label = new Label(Format(frame));
            label.AddToClassList(FrameClass);

            if (frame.HasLocation)
            {
                label.AddToClassList(FrameLinkClass);
                label.tooltip = "Click to preview, double-click to open " + frame.FilePath + " at line " + frame.Line + ".";
                label.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount >= 2)
                    {
                        FrameActivated?.Invoke(frame);
                    }
                    else
                    {
                        SelectFrame(frame);
                    }
                });
            }

            if (isEntry)
            {
                label.AddToClassList(FrameEntryClass);
            }

            return label;
        }
    }
}
