using System;
using System.Runtime.CompilerServices;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    /// <summary>
    /// [THEORETICAL MAXIMUM SAFE PERFORMANCE]
    /// A GC-free, stack-allocated, Span-based circular buffer designed for extreme performance under safe C# context.
    /// The user provides the backing memory via a Span, ensuring safety and control.
    /// </summary>
    /// <typeparam name="T">Any type. For best performance, use unmanaged types like float, int, etc.</typeparam>
    public ref struct CircularBuffer<T>
    {
        private readonly Span<T> _buffer;
        private readonly int _capacityMask;
        private int _head;
        private int _tail;

        public int Count { get; private set; }
        public int Capacity => _buffer.Length;
        public bool IsEmpty => Count == 0;
        public bool IsFull => Count == Capacity;

        /// <summary>
        /// Creates a circular buffer on top of a pre-allocated memory block represented by a Span.
        /// </summary>
        /// <param name="buffer">The Span representing the memory block. The length MUST be a power of two.</param>
        public CircularBuffer(Span<T> buffer)
        {
            int capacity = buffer.Length;
            if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
            {
                throw new ArgumentException("Capacity (from buffer.Length) must be a positive power of two.", nameof(buffer));
            }

            _buffer = buffer;
            _capacityMask = capacity - 1;
            _head = -1;
            _tail = 0;
            Count = 0;
        }

        /// <summary>
        /// Adds an item. Overwrites the oldest item if full.
        /// This method is aggressively inlined and uses bitwise operations for peak performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            _head = (_head + 1) & _capacityMask;
            _buffer[_head] = item;

            if (IsFull)
            {
                _tail = (_tail + 1) & _capacityMask;
            }
            else
            {
                Count++;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ReadOnlySpan<T> items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Add(items[i]);
            }
        }

        /// <summary>
        /// [ZERO-COPY READ] Gets the buffer's content as one or two ReadOnlySpans.
        /// This is the most performant way to read data, involving no allocation or copying.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetSpans(out ReadOnlySpan<T> first, out ReadOnlySpan<T> second)
        {
            if (IsEmpty)
            {
                first = second = ReadOnlySpan<T>.Empty;
                return;
            }

            if (_tail <= _head)
            {
                // Data is in a single contiguous block
                first = _buffer.Slice(_tail, Count);
                second = ReadOnlySpan<T>.Empty;
            }
            else
            {
                // Data wraps around
                first = _buffer.Slice(_tail, Capacity - _tail);
                second = _buffer.Slice(0, _head + 1);
            }
        }

        /// <summary>
        /// Clears the buffer. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _head = -1;
            _tail = 0;
            Count = 0;
        }
    }
}