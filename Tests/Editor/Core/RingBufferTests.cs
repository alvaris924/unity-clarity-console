using System;
using System.Linq;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    public sealed class RingBufferTests
    {
        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(-1));
        }

        [Test]
        public void Append_BelowCapacity_PreservesInsertionOrder()
        {
            var buffer = new RingBuffer<int>(4);

            buffer.Append(1);
            buffer.Append(2);
            buffer.Append(3);

            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(buffer.Overwritten, Is.EqualTo(0));
        }

        [Test]
        public void Append_PastCapacity_OverwritesOldest()
        {
            var buffer = new RingBuffer<int>(3);

            for (int i = 1; i <= 5; i++)
            {
                buffer.Append(i);
            }

            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.ToArray(), Is.EqualTo(new[] { 3, 4, 5 }));
            Assert.That(buffer[0], Is.EqualTo(3));
            Assert.That(buffer.Appended, Is.EqualTo(5));
            Assert.That(buffer.Overwritten, Is.EqualTo(2));
        }

        [Test]
        public void Append_ReportsTheEvictedItem()
        {
            var buffer = new RingBuffer<int>(2);

            Assert.That(buffer.Append(1, out int evicted), Is.False);
            Assert.That(buffer.Append(2, out evicted), Is.False);
            Assert.That(buffer.Append(3, out evicted), Is.True);
            Assert.That(evicted, Is.EqualTo(1));
        }

        [Test]
        public void Indexer_OutsideRetainedRange_Throws()
        {
            var buffer = new RingBuffer<int>(2);
            buffer.Append(7);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[-1]);
        }

        [Test]
        public void Clear_ResetsCountAndCounters()
        {
            var buffer = new RingBuffer<string>(2);
            buffer.Append("a");
            buffer.Append("b");
            buffer.Append("c");

            buffer.Clear();

            Assert.That(buffer.Count, Is.EqualTo(0));
            Assert.That(buffer.Appended, Is.EqualTo(0));
            Assert.That(buffer.Overwritten, Is.EqualTo(0));

            buffer.Append("d");
            Assert.That(buffer.ToArray(), Is.EqualTo(new[] { "d" }));
        }

        [Test]
        public void Enumeration_MatchesIndexerAfterWraparound()
        {
            var buffer = new RingBuffer<int>(3);

            for (int i = 0; i < 10; i++)
            {
                buffer.Append(i);
            }

            int[] viaIndexer = Enumerable.Range(0, buffer.Count).Select(i => buffer[i]).ToArray();

            Assert.That(buffer.ToArray(), Is.EqualTo(viaIndexer));
            Assert.That(viaIndexer, Is.EqualTo(new[] { 7, 8, 9 }));
        }
    }
}
