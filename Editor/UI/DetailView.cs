using System;
using ClarityConsole.Core;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The pane under the list: the selected entry's message in a copyable field, then one row per
    /// stack frame. Frames with a location are links; the entry frame is emphasized.
    /// </summary>
    internal sealed class DetailView : ScrollView
    {
        public const string FrameClass = "cc-frame";
        public const string FrameLinkClass = "cc-frame-link";
        public const string FrameEntryClass = "cc-frame-entry";

        private readonly TextField _message;
        private readonly VisualElement _frames;

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
        }

        /// <summary>Raised when the user clicks a frame that has a location.</summary>
        public event Action<TraceFrame> FrameActivated;

        public int FrameRowCount => _frames.childCount;

        public void Show(LogEntry entry)
        {
            _frames.Clear();

            if (entry == null)
            {
                _message.SetValueWithoutNotify(string.Empty);
                return;
            }

            _message.SetValueWithoutNotify(entry.Message);

            ParsedTrace trace = entry.Trace;
            foreach (TraceFrame frame in trace.Frames)
            {
                _frames.Add(BuildRow(frame, ReferenceEquals(frame, trace.EntryFrame)));
            }
        }

        internal static string Format(TraceFrame frame)
        {
            return frame.HasLocation ? frame.Signature + "    " + frame.FilePath + ":" + frame.Line : frame.Signature;
        }

        private VisualElement BuildRow(TraceFrame frame, bool isEntry)
        {
            var label = new Label(Format(frame));
            label.AddToClassList(FrameClass);

            if (frame.HasLocation)
            {
                label.AddToClassList(FrameLinkClass);
                label.tooltip = "Open " + frame.FilePath + " at line " + frame.Line;
                label.RegisterCallback<ClickEvent>(_ => FrameActivated?.Invoke(frame));
            }

            if (isEntry)
            {
                label.AddToClassList(FrameEntryClass);
            }

            return label;
        }
    }
}
