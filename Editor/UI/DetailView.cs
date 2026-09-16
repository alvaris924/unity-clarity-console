using System;
using System.Collections.Generic;
using ClarityConsole.Core;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The pane under the list: the selected entry's message, its stack frames with infrastructure runs
    /// folded away, and a preview of the source around the selected frame. Clicking a frame selects it and
    /// moves the preview; double-clicking it, or the preview, opens the file in the code editor.
    /// </summary>
    internal sealed class DetailView : ScrollView
    {
        public const string FrameClass = "cc-frame";
        public const string FrameLinkClass = "cc-frame-link";
        public const string FrameEntryClass = "cc-frame-entry";
        public const string FrameSelectedClass = "cc-frame-selected";
        public const string FrameHiddenClass = "cc-frame-hidden";

        private readonly TextField _message;
        private readonly VisualElement _frames;
        private readonly SourcePreview _preview;
        private readonly Dictionary<TraceFrame, Label> _rows = new Dictionary<TraceFrame, Label>();
        private readonly HashSet<int> _expanded = new HashSet<int>();
        private List<FrameGroup> _groups = new List<FrameGroup>();
        private LogEntry _entry;
        private TraceFrame _entryFrame;

        public DetailView()
            : base(ScrollViewMode.Vertical)
        {
            AddToClassList("cc-detail");

            _message = new TextField { multiline = true, isReadOnly = true };
            _message.AddToClassList("cc-detail-message");
            _message.AddToClassList("cc-mono");
            Add(_message);

            _frames = new VisualElement();
            _frames.AddToClassList("cc-frames");
            Add(_frames);

            _preview = new SourcePreview();
            _preview.Activated += OpenSelectedFrame;
            Add(_preview);
        }

        /// <summary>Raised when a frame should be opened in the code editor.</summary>
        public event Action<TraceFrame> FrameActivated;

        /// <summary>Asks for the source around a frame. Returning null hides the preview.</summary>
        public Func<TraceFrame, SourceSnippet> SnippetProvider { get; set; }

        /// <summary>Which frames count as infrastructure and are folded away. Null folds nothing.</summary>
        public FrameFilter FrameFilter { get; set; }

        /// <summary>The frame the preview is showing, or null.</summary>
        public TraceFrame SelectedFrame { get; private set; }

        /// <summary>Rows currently drawn, folded runs included.</summary>
        public int FrameRowCount => _frames.childCount;

        /// <summary>Number of frames not drawn because their run is folded.</summary>
        public int HiddenFrameCount
        {
            get
            {
                int hidden = 0;
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].IsNoise && !_expanded.Contains(i))
                    {
                        hidden += _groups[i].Count;
                    }
                }

                return hidden;
            }
        }

        public bool IsPreviewVisible => _preview.style.display.value == DisplayStyle.Flex;

        public void Show(LogEntry entry)
        {
            _entry = entry;
            _entryFrame = null;
            _expanded.Clear();
            SelectedFrame = null;
            _preview.Hide();

            if (entry == null)
            {
                _groups = new List<FrameGroup>();
                _message.SetValueWithoutNotify(string.Empty);
                _frames.Clear();
                _rows.Clear();
                return;
            }

            _message.SetValueWithoutNotify(entry.Message);
            _entryFrame = FrameGrouper.FindEntryFrame(entry.Trace, FrameFilter);
            _groups = FrameGrouper.Group(entry.Trace, FrameFilter);
            RenderFrames();
            SelectFrame(_entryFrame);
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

        /// <summary>Unfolds a folded run so its frames become rows of their own.</summary>
        public void ExpandGroup(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= _groups.Count || !_groups[groupIndex].IsNoise || !_expanded.Add(groupIndex))
            {
                return;
            }

            TraceFrame selected = SelectedFrame;
            RenderFrames();
            if (selected != null && _rows.TryGetValue(selected, out Label row))
            {
                row.AddToClassList(FrameSelectedClass);
            }
        }

        internal static string Format(TraceFrame frame)
        {
            return frame.HasLocation ? frame.Signature + "    " + frame.FilePath + ":" + frame.Line : frame.Signature;
        }

        /// <summary>"3 frames hidden (UnityEngine, Cysharp)" for a folded run.</summary>
        internal static string SummariseHidden(FrameGroup group)
        {
            var roots = new List<string>();
            foreach (TraceFrame frame in group.Frames)
            {
                string root = RootNamespace(frame.TypeName);
                if (root.Length > 0 && !roots.Contains(root))
                {
                    roots.Add(root);
                    if (roots.Count == 2)
                    {
                        break;
                    }
                }
            }

            string count = group.Count + (group.Count == 1 ? " frame hidden" : " frames hidden");
            return roots.Count == 0 ? count : count + " (" + string.Join(", ", roots) + ")";
        }

        private static string RootNamespace(string typeName)
        {
            int dot = typeName.IndexOf('.');
            return dot > 0 ? typeName.Substring(0, dot) : typeName;
        }

        private void RenderFrames()
        {
            _frames.Clear();
            _rows.Clear();

            for (int i = 0; i < _groups.Count; i++)
            {
                FrameGroup group = _groups[i];
                if (group.IsNoise && !_expanded.Contains(i))
                {
                    _frames.Add(BuildHiddenRow(group, i));
                    continue;
                }

                foreach (TraceFrame frame in group.Frames)
                {
                    Label row = BuildRow(frame, ReferenceEquals(frame, _entryFrame));
                    _rows[frame] = row;
                    _frames.Add(row);
                }
            }
        }

        private void OpenSelectedFrame()
        {
            if (SelectedFrame != null && SelectedFrame.HasLocation)
            {
                FrameActivated?.Invoke(SelectedFrame);
            }
        }

        private Label BuildHiddenRow(FrameGroup group, int groupIndex)
        {
            var label = new Label(SummariseHidden(group));
            label.AddToClassList(FrameClass);
            label.AddToClassList(FrameHiddenClass);
            label.tooltip = "Infrastructure frames. Click to show them.";
            label.RegisterCallback<ClickEvent>(_ => ExpandGroup(groupIndex));
            return label;
        }

        private Label BuildRow(TraceFrame frame, bool isEntry)
        {
            var label = new Label(Format(frame));
            label.AddToClassList(FrameClass);
            label.AddToClassList("cc-mono");

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
