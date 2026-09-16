using System;
using ClarityConsole.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// A thin strip above the list showing how many entries landed in each slice of time, stacked by
    /// severity, so a burst of errors is visible at a glance. Clicking a bar jumps the list to the first
    /// entry in that slice. Drawn with the mesh API in one pass; nothing is allocated per entry.
    /// </summary>
    internal sealed class TimelineStrip : VisualElement
    {
        public const int DefaultBucketCount = 120;

        private static readonly Color LogColor = new Color(0.55f, 0.75f, 1f, 0.85f);
        private static readonly Color WarningColor = new Color(0.96f, 0.65f, 0.14f, 0.9f);
        private static readonly Color ErrorColor = new Color(1f, 0.42f, 0.42f, 0.95f);

        private TimelineBucket[] _buckets = Array.Empty<TimelineBucket>();
        private int _peak;
        private DateTime _start;
        private DateTime _end;
        private int _hovered = -1;

        public TimelineStrip()
        {
            AddToClassList("cc-timeline");
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(_ => SetHovered(-1));
            RegisterCallback<ClickEvent>(OnClick);
        }

        /// <summary>Raised with the index into the source list of the first entry in the clicked slice.</summary>
        public event Action<int> SliceActivated;

        public int BucketCount { get; set; } = DefaultBucketCount;

        /// <summary>The buckets currently drawn. Test hook.</summary>
        public TimelineBucket[] Buckets => _buckets;

        /// <summary>Recomputes the buckets from the rows currently shown.</summary>
        public void Refresh(System.Collections.Generic.IReadOnlyList<LogEntry> entries)
        {
            _buckets = TimelineBuckets.Build(entries, BucketCount, out _start, out _end);
            _peak = TimelineBuckets.Peak(_buckets);
            style.display = _peak > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            tooltip = _peak > 0
                ? _start.ToLocalTime().ToString("HH:mm:ss") + " to " + _end.ToLocalTime().ToString("HH:mm:ss") + ". Click a bar to jump there."
                : string.Empty;
            MarkDirtyRepaint();
        }

        /// <summary>Which slice a horizontal position falls in, or -1 outside the strip.</summary>
        internal int SliceAt(float x)
        {
            float width = resolvedStyle.width;
            if (_buckets.Length == 0 || float.IsNaN(width) || width <= 0 || x < 0 || x >= width)
            {
                return -1;
            }

            return Math.Min(_buckets.Length - 1, (int)(x / width * _buckets.Length));
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (_peak == 0 || _buckets.Length == 0)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            float width = contentRect.width;
            float height = contentRect.height;
            float slot = width / _buckets.Length;
            float bar = Math.Max(1f, slot - 1f);

            for (int i = 0; i < _buckets.Length; i++)
            {
                TimelineBucket bucket = _buckets[i];
                if (bucket.Total == 0)
                {
                    continue;
                }

                float x = i * slot;
                float unit = height / _peak;
                float y = height;

                y = DrawSegment(painter, x, y, bar, bucket.Logs * unit, LogColor, i == _hovered);
                y = DrawSegment(painter, x, y, bar, bucket.Warnings * unit, WarningColor, i == _hovered);
                DrawSegment(painter, x, y, bar, bucket.Errors * unit, ErrorColor, i == _hovered);
            }
        }

        private static float DrawSegment(Painter2D painter, float x, float bottom, float width, float height, Color color, bool highlighted)
        {
            if (height <= 0)
            {
                return bottom;
            }

            float top = bottom - height;
            painter.fillColor = highlighted ? Color.Lerp(color, Color.white, 0.35f) : color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, bottom));
            painter.LineTo(new Vector2(x + width, bottom));
            painter.LineTo(new Vector2(x + width, top));
            painter.LineTo(new Vector2(x, top));
            painter.ClosePath();
            painter.Fill();
            return top;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            SetHovered(SliceAt(evt.localPosition.x));
        }

        private void SetHovered(int slice)
        {
            if (_hovered == slice)
            {
                return;
            }

            _hovered = slice;
            MarkDirtyRepaint();
        }

        private void OnClick(ClickEvent evt)
        {
            int slice = SliceAt(evt.localPosition.x);
            if (slice < 0)
            {
                return;
            }

            // An empty slice jumps to the nearest earlier one with entries, so clicking a gap still lands somewhere.
            for (int i = slice; i >= 0; i--)
            {
                if (_buckets[i].FirstIndex >= 0)
                {
                    SliceActivated?.Invoke(_buckets[i].FirstIndex);
                    return;
                }
            }
        }
    }
}
