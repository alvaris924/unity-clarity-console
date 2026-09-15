using System;
using System.Collections.Generic;
using System.IO;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Reads source files for the preview pane and keeps the most recent ones in memory, keyed by path and
    /// last write time so an edited file is re-read. Only the selected entry's file is ever touched, and
    /// files past <see cref="MaxFileBytes"/> are skipped rather than pulled into memory.
    /// </summary>
    internal sealed class SourceCache
    {
        public const int DefaultRadius = 3;
        public const long MaxFileBytes = 4 * 1024 * 1024;

        private const int MaxFiles = 16;

        private readonly Dictionary<string, Entry> _files = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new List<string>();

        /// <summary>Number of files held in memory. Test hook.</summary>
        public int CachedFileCount => _files.Count;

        /// <summary>
        /// Returns the lines around <paramref name="line"/>, or null when the file cannot be read, is too
        /// large, or the line is not a real line number.
        /// </summary>
        public SourceSnippet TryGet(string absolutePath, int line, int radius = DefaultRadius)
        {
            if (string.IsNullOrEmpty(absolutePath) || line <= 0 || radius < 0)
            {
                return null;
            }

            string[] lines = TryGetLines(absolutePath);
            if (lines == null || lines.Length == 0)
            {
                return null;
            }

            int index = line - 1;
            if (index >= lines.Length)
            {
                // The file has changed since the log was written, so any window would be misleading.
                return null;
            }

            int first = Math.Max(0, index - radius);
            int last = Math.Min(lines.Length - 1, index + radius);
            var window = new List<string>(last - first + 1);
            for (int i = first; i <= last; i++)
            {
                window.Add(lines[i]);
            }

            int highlight = index >= first && index <= last ? index - first : -1;
            return new SourceSnippet(absolutePath, first + 1, window, highlight);
        }

        public void Clear()
        {
            _files.Clear();
            _order.Clear();
        }

        private string[] TryGetLines(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > MaxFileBytes)
                {
                    return null;
                }

                if (_files.TryGetValue(path, out Entry cached) && cached.WrittenUtc == info.LastWriteTimeUtc)
                {
                    Touch(path);
                    return cached.Lines;
                }

                string[] lines = File.ReadAllLines(path);
                _files[path] = new Entry(lines, info.LastWriteTimeUtc);
                Touch(path);

                while (_order.Count > MaxFiles)
                {
                    _files.Remove(_order[0]);
                    _order.RemoveAt(0);
                }

                return lines;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        private void Touch(string path)
        {
            _order.Remove(path);
            _order.Add(path);
        }

        private readonly struct Entry
        {
            public Entry(string[] lines, DateTime writtenUtc)
            {
                Lines = lines;
                WrittenUtc = writtenUtc;
            }

            public string[] Lines { get; }

            public DateTime WrittenUtc { get; }
        }
    }
}
