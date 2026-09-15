using System;
using System.IO;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class SourceCacheTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ClarityConsoleSourceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void TryGet_ReturnsTheWindowAroundTheLine()
        {
            string path = WriteFile("a.cs", 10);
            var cache = new SourceCache();

            SourceSnippet snippet = cache.TryGet(path, line: 5, radius: 2);

            Assert.That(snippet.FirstLine, Is.EqualTo(3));
            Assert.That(snippet.Lines, Is.EqualTo(new[] { "line 3", "line 4", "line 5", "line 6", "line 7" }));
            Assert.That(snippet.HighlightIndex, Is.EqualTo(2));
            Assert.That(snippet.HighlightLine, Is.EqualTo(5));
            Assert.That(snippet.FilePath, Is.EqualTo(path));
        }

        [Test]
        public void TryGet_ClampsAtTheStartAndEndOfTheFile()
        {
            string path = WriteFile("a.cs", 4);
            var cache = new SourceCache();

            SourceSnippet first = cache.TryGet(path, line: 1, radius: 3);
            Assert.That(first.FirstLine, Is.EqualTo(1));
            Assert.That(first.Lines.Count, Is.EqualTo(4));
            Assert.That(first.HighlightIndex, Is.EqualTo(0));

            SourceSnippet last = cache.TryGet(path, line: 4, radius: 3);
            Assert.That(last.FirstLine, Is.EqualTo(1));
            Assert.That(last.HighlightLine, Is.EqualTo(4));
        }

        [Test]
        public void TryGet_LineBeyondTheFile_ReturnsNull()
        {
            // The file was edited after the log was written, so any window would point at the wrong code.
            string path = WriteFile("a.cs", 3);

            Assert.That(new SourceCache().TryGet(path, line: 99, radius: 1), Is.Null);
        }

        [Test]
        public void TryGet_ZeroRadius_ReturnsOnlyThatLine()
        {
            string path = WriteFile("a.cs", 5);

            SourceSnippet snippet = new SourceCache().TryGet(path, line: 3, radius: 0);

            Assert.That(snippet.Lines, Is.EqualTo(new[] { "line 3" }));
        }

        [TestCase(0)]
        [TestCase(-4)]
        public void TryGet_InvalidLine_ReturnsNull(int line)
        {
            Assert.That(new SourceCache().TryGet(WriteFile("a.cs", 3), line, 2), Is.Null);
        }

        [Test]
        public void TryGet_MissingFileOrEmptyPath_ReturnsNull()
        {
            var cache = new SourceCache();

            Assert.That(cache.TryGet(Path.Combine(_directory, "nope.cs"), 1), Is.Null);
            Assert.That(cache.TryGet(string.Empty, 1), Is.Null);
            Assert.That(cache.TryGet(null, 1), Is.Null);
            Assert.That(cache.CachedFileCount, Is.EqualTo(0));
        }

        [Test]
        public void TryGet_CachesByPath_AndRereadsAfterAnEdit()
        {
            string path = WriteFile("a.cs", 3);
            var cache = new SourceCache();
            Assert.That(cache.TryGet(path, 1).Lines[0], Is.EqualTo("line 1"));
            Assert.That(cache.CachedFileCount, Is.EqualTo(1));

            cache.TryGet(path, 2);
            Assert.That(cache.CachedFileCount, Is.EqualTo(1), "the same file is not cached twice");

            File.WriteAllLines(path, new[] { "edited 1", "edited 2", "edited 3" });
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

            Assert.That(cache.TryGet(path, 1).Lines[0], Is.EqualTo("edited 1"));
        }

        [Test]
        public void Cache_KeepsOnlyTheMostRecentFiles()
        {
            var cache = new SourceCache();

            for (int i = 0; i < 40; i++)
            {
                cache.TryGet(WriteFile("file" + i + ".cs", 2), 1);
            }

            Assert.That(cache.CachedFileCount, Is.LessThanOrEqualTo(16));
        }

        [Test]
        public void Clear_EmptiesTheCache()
        {
            var cache = new SourceCache();
            cache.TryGet(WriteFile("a.cs", 2), 1);

            cache.Clear();

            Assert.That(cache.CachedFileCount, Is.EqualTo(0));
        }

        private string WriteFile(string name, int lines)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllLines(path, Enumerable.Range(1, lines).Select(i => "line " + i));
            return path;
        }
    }
}
