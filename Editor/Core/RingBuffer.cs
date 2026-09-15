using System;
using System.Collections;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Fixed-capacity circular buffer. Appending past capacity overwrites the oldest item,
    /// so index 0 is always the oldest item still retained. Not thread-safe by design:
    /// the capture layer drains its queue onto the buffer from the main thread only.
    /// </summary>
    internal sealed class RingBuffer<T> : IReadOnlyList<T>
    {
        private readonly T[] _items;
        private int _head;
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            }

            _items = new T[capacity];
        }

        /// <summary>Maximum number of items retained.</summary>
        public int Capacity => _items.Length;

        /// <summary>Number of items currently retained.</summary>
        public int Count => _count;

        /// <summary>Total number of items appended since the last clear, including overwritten ones.</summary>
        public long Appended { get; private set; }

        /// <summary>Number of items lost to overwriting since the last clear.</summary>
        public long Overwritten => Appended - _count;

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside the retained range.");
                }

                return _items[(_head + index) % _items.Length];
            }
        }

        public void Append(T item)
        {
            Append(item, out _);
        }

        /// <summary>
        /// Appends an item. Returns true when an older item was overwritten, in which case
        /// <paramref name="evicted"/> holds it so callers can keep derived counts in step.
        /// </summary>
        public bool Append(T item, out T evicted)
        {
            bool overwrote = _count == _items.Length;

            if (overwrote)
            {
                evicted = _items[_head];
                _items[_head] = item;
                _head = (_head + 1) % _items.Length;
            }
            else
            {
                evicted = default;
                _items[(_head + _count) % _items.Length] = item;
                _count++;
            }

            Appended++;
            return overwrote;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
            Appended = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
