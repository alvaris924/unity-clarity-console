using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Append-only, segmented on-disk copy of the store so entries survive domain reloads and Editor
    /// restarts. Each segment starts with a header; each record is a length, a CRC-32 and a payload
    /// written by <see cref="JournalRecord"/>. A torn tail is truncated on load and everything before it
    /// is kept. Segments rotate at <see cref="MaxSegmentBytes"/>; only the newest <see cref="MaxSegments"/>
    /// are retained, which bounds both disk use and the time a reload spends reading.
    /// Main thread only.
    /// </summary>
    internal sealed class LogJournal : IDisposable
    {
        public const int DefaultMaxSegmentBytes = 8 * 1024 * 1024;
        public const int DefaultMaxSegments = 4;

        private const string Magic = "CCJ1";
        private const int FormatVersion = 1;
        private const string FilePrefix = "journal-";
        private const string FileExtension = ".bin";
        private const int HeaderBytes = 8;
        private const int MaxRecordBytes = 16 * 1024 * 1024;

        private readonly string _directory;
        private readonly MemoryStream _payload = new MemoryStream();
        private readonly BinaryWriter _payloadWriter;
        private FileStream _stream;
        private BinaryWriter _writer;
        private int _currentIndex;
        private long _currentLength;
        private long _olderSegmentBytes;
        private bool _dirty;

        public LogJournal(string directory, int maxSegmentBytes = DefaultMaxSegmentBytes, int maxSegments = DefaultMaxSegments)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("A journal directory is required.", nameof(directory));
            }

            if (maxSegmentBytes <= HeaderBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSegmentBytes));
            }

            if (maxSegments <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSegments));
            }

            _directory = directory;
            MaxSegmentBytes = maxSegmentBytes;
            MaxSegments = maxSegments;
            _payloadWriter = new BinaryWriter(_payload, Encoding.UTF8, leaveOpen: true);
        }

        public string Directory => _directory;

        public int MaxSegmentBytes { get; }

        public int MaxSegments { get; }

        /// <summary>Bytes currently on disk across retained segments, including unflushed writes.</summary>
        public long SizeBytes => _olderSegmentBytes + _currentLength;

        /// <summary>
        /// Reads every retained entry, oldest first, and positions the writer at the end of the newest
        /// segment. Segments beyond <see cref="MaxSegments"/> are deleted; a torn or foreign newest segment
        /// is truncated or discarded so appending can continue.
        /// </summary>
        public List<LogEntry> Load(bool dropContexts)
        {
            CloseWriter();
            var entries = new List<LogEntry>();
            List<KeyValuePair<int, string>> segments = ListSegments();

            while (segments.Count > MaxSegments)
            {
                TryDelete(segments[0].Value);
                segments.RemoveAt(0);
            }

            _olderSegmentBytes = 0;
            _currentIndex = 0;
            _currentLength = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                bool newest = i == segments.Count - 1;
                long usableBytes = ReadSegment(segments[i].Value, entries, dropContexts, newest);

                if (newest)
                {
                    if (usableBytes < 0)
                    {
                        // Foreign or headerless file: discard it and continue in a fresh segment after it.
                        TryDelete(segments[i].Value);
                        _currentIndex = segments[i].Key + 1;
                        _currentLength = 0;
                    }
                    else
                    {
                        _currentIndex = segments[i].Key;
                        _currentLength = usableBytes;
                    }
                }
                else if (usableBytes > 0)
                {
                    _olderSegmentBytes += usableBytes;
                }
            }

            return entries;
        }

        public void Append(LogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (_writer == null)
            {
                OpenCurrentSegment();
            }

            if (_currentLength >= MaxSegmentBytes)
            {
                Rotate();
            }

            _payload.SetLength(0);
            JournalRecord.Write(_payloadWriter, entry);
            _payloadWriter.Flush();

            int length = (int)_payload.Length;
            byte[] buffer = _payload.GetBuffer();
            _writer.Write(length);
            _writer.Write(Crc32.Compute(buffer, 0, length));
            _writer.Write(buffer, 0, length);
            _currentLength += 8 + length;
            _dirty = true;
        }

        /// <summary>Pushes buffered records to the operating system. Call once per drain, not per entry.</summary>
        public void Flush()
        {
            if (_dirty && _writer != null)
            {
                _writer.Flush();
                _stream.Flush();
                _dirty = false;
            }
        }

        /// <summary>Deletes every segment. The next append starts a fresh journal.</summary>
        public void Reset()
        {
            CloseWriter();
            foreach (KeyValuePair<int, string> segment in ListSegments())
            {
                TryDelete(segment.Value);
            }

            _currentIndex = 0;
            _currentLength = 0;
            _olderSegmentBytes = 0;
        }

        public void Dispose()
        {
            CloseWriter();
            _payloadWriter.Dispose();
            _payload.Dispose();
        }

        private void Rotate()
        {
            CloseWriter();
            _olderSegmentBytes += _currentLength;
            _currentIndex++;
            _currentLength = 0;
            OpenCurrentSegment();
            PruneOldSegments();
        }

        private void OpenCurrentSegment()
        {
            if (_currentIndex == 0)
            {
                _currentIndex = 1;
            }

            System.IO.Directory.CreateDirectory(_directory);
            string path = SegmentPath(_currentIndex);
            bool fresh = !File.Exists(path) || new FileInfo(path).Length < HeaderBytes;
            _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 64 * 1024);
            _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            if (fresh)
            {
                _writer.Write(Encoding.ASCII.GetBytes(Magic));
                _writer.Write(FormatVersion);
                _currentLength = HeaderBytes;
                _dirty = true;
            }
        }

        private void PruneOldSegments()
        {
            List<KeyValuePair<int, string>> segments = ListSegments();
            _olderSegmentBytes = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments.Count - i > MaxSegments)
                {
                    TryDelete(segments[i].Value);
                }
                else if (segments[i].Key != _currentIndex)
                {
                    _olderSegmentBytes += new FileInfo(segments[i].Value).Length;
                }
            }
        }

        /// <summary>Returns the number of valid bytes in the segment, or -1 when the header is not ours.</summary>
        private static long ReadSegment(string path, List<LogEntry> entries, bool dropContexts, bool truncateOnError)
        {
            long goodBytes;
            using (var stream = new FileStream(path, FileMode.Open, truncateOnError ? FileAccess.ReadWrite : FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                if (stream.Length < HeaderBytes
                    || Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic
                    || reader.ReadInt32() != FormatVersion)
                {
                    return -1;
                }

                goodBytes = HeaderBytes;
                while (stream.Position < stream.Length)
                {
                    if (!TryReadRecord(stream, reader, entries, dropContexts))
                    {
                        break;
                    }

                    goodBytes = stream.Position;
                }

                if (truncateOnError && goodBytes < stream.Length)
                {
                    stream.SetLength(goodBytes);
                }
            }

            return goodBytes;
        }

        private static bool TryReadRecord(FileStream stream, BinaryReader reader, List<LogEntry> entries, bool dropContexts)
        {
            if (stream.Length - stream.Position < 8)
            {
                return false;
            }

            int length = reader.ReadInt32();
            uint crc = reader.ReadUInt32();
            if (length <= 0 || length > MaxRecordBytes || stream.Length - stream.Position < length)
            {
                return false;
            }

            byte[] payload = reader.ReadBytes(length);
            if (payload.Length != length || Crc32.Compute(payload, 0, length) != crc)
            {
                return false;
            }

            try
            {
                using (var payloadReader = new BinaryReader(new MemoryStream(payload, false), Encoding.UTF8))
                {
                    entries.Add(JournalRecord.Read(payloadReader, dropContexts));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException)
            {
                return false;
            }

            return true;
        }

        private List<KeyValuePair<int, string>> ListSegments()
        {
            var segments = new List<KeyValuePair<int, string>>();
            if (!System.IO.Directory.Exists(_directory))
            {
                return segments;
            }

            foreach (string path in System.IO.Directory.GetFiles(_directory, FilePrefix + "*" + FileExtension))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(name.Substring(FilePrefix.Length), out int index))
                {
                    segments.Add(new KeyValuePair<int, string>(index, path));
                }
            }

            segments.Sort((a, b) => a.Key.CompareTo(b.Key));
            return segments;
        }

        private string SegmentPath(int index)
        {
            return Path.Combine(_directory, FilePrefix + index.ToString("D6") + FileExtension);
        }

        private void CloseWriter()
        {
            if (_writer == null)
            {
                return;
            }

            Flush();
            _writer.Dispose();
            _stream.Dispose();
            _writer = null;
            _stream = null;
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
