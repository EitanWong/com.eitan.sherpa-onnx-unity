using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Eitan.SherpaONNXUnity.Runtime
{
    /// <summary>
    /// High-performance circular buffer with SPSC (single-producer/single-consumer) semantics.
    /// - Lock-free: Uses Volatile.Read/Write for cross-thread visibility (SPSC only).
    /// - Zero-GC hot path: No managed allocations during Read/Write operations.
    /// - Power-of-two capacity fast-path: mask-based wrap without modulo.
    /// - Alignment (FrameStride): All operations align to this granularity (e.g., channels).
    ///
    /// ⚠️ Safety contract: exactly one producer thread must call Write/TryWriteExact,
    /// and exactly one consumer thread must call Read/TryReadExact/Peek/Skip/TryCopyLastExact.
    /// </summary>
    public sealed class CircularBuffer<T>
    {
        /// <summary>Public usable capacity (in elements); internal storage is Capacity + 1 to distinguish full/empty.</summary>
        public int Capacity { get; }

        /// <summary>Alignment (granularity) for all operations. For audio, use number of channels.</summary>
        public int Alignment => _alignment;

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { int w = Volatile.Read(ref _writePos.Value); int r = Volatile.Read(ref _readPos.Value); return w == r; }
        }

        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { int w = Volatile.Read(ref _writePos.Value); int r = Volatile.Read(ref _readPos.Value); return Advance(w, 1) == r; }
        }

        /// <summary>Approximate readable count (aligned to Alignment).</summary>
        public int ReadableCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int w = Volatile.Read(ref _writePos.Value);
                int r = Volatile.Read(ref _readPos.Value);
                int readable = AvailableToRead(w, r);
                return readable - (readable % _alignment);
            }
        }

        /// <summary>Approximate writable count (aligned to Alignment).</summary>
        public int WritableCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int w = Volatile.Read(ref _writePos.Value);
                int r = Volatile.Read(ref _readPos.Value);
                int writable = AvailableToWrite(w, r);
                return writable - (writable % _alignment);
            }
        }

        private readonly int _size;      // internal array length = Capacity + 1
        private readonly bool _useMask;
        private readonly int _mask;      // valid only when _useMask == true
        private readonly T[] _buffer;
        private readonly int _alignment;

        // Padded indices to mitigate false sharing (approx. 64B cache line separation)
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PaddedInt
        {
            public int Value;
#pragma warning disable IDE0051
            private long _pad1, _pad2, _pad3, _pad4, _pad5, _pad6, _pad7;
#pragma warning restore IDE0051
        }

        private PaddedInt _writePos; // producer-side index (Volatile only)
        private PaddedInt _readPos;  // consumer-side index (Volatile only)

        /// <summary>
        /// Create a heap-safe circular buffer.
        /// </summary>
        /// <param name="capacity">Target usable capacity (elements). Will be aligned and expanded internally.</param>
        /// <param name="alignment">Granularity to align all operations (>=1). For audio, use channel count.</param>
        public CircularBuffer(int capacity, int alignment = 1)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            }


            if (alignment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be positive.");
            }


            _alignment = alignment;
            int alignedCap = AlignCapacity(capacity);
            _size = NextPowerOfTwo(Math.Max(2, alignedCap + 1));
            Capacity = _size - 1; // one slot reserved to distinguish full from empty
            _buffer = new T[_size];

            if (IsPowerOfTwo(_size)) { _useMask = true; _mask = _size - 1; } else { _useMask = false; _mask = 0; }

            Volatile.Write(ref _writePos.Value, 0);
            Volatile.Write(ref _readPos.Value, 0);
        }

        // ----------------------------- Producer API -----------------------------
        /// <summary>
        /// Producer writes as many elements as fit (aligned). Returns number actually written.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(ReadOnlySpan<T> data)
        {
            if (data.IsEmpty)
            {
                return 0;
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);

            int writable = AvailableToWrite(w, r);
            int toWrite = Math.Min(data.Length, writable);
            toWrite -= toWrite % _alignment;
            if (toWrite <= 0)
            {
                return 0;
            }

            int first = Math.Min(toWrite, _size - w);
            data.Slice(0, first).CopyTo(new Span<T>(_buffer, w, first));
            int second = toWrite - first;
            if (second > 0)
            {
                data.Slice(first, second).CopyTo(new Span<T>(_buffer, 0, second));
            }


            Volatile.Write(ref _writePos.Value, Advance(w, toWrite));
            return toWrite;
        }

        /// <summary>
        /// Producer attempts to write exactly <paramref name="data"/>.Length elements; returns false if insufficient space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteExact(ReadOnlySpan<T> data)
        {
            if (data.IsEmpty)
            {
                return true;
            }


            if ((data.Length % _alignment) != 0)
            {
                throw new ArgumentException("Data length must align with Alignment.", nameof(data));
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);

            int writable = AvailableToWrite(w, r);
            if (writable < data.Length)
            {
                return false;
            }


            int toWrite = data.Length;
            int first = Math.Min(toWrite, _size - w);
            data.Slice(0, first).CopyTo(new Span<T>(_buffer, w, first));
            int second = toWrite - first;
            if (second > 0)
            {
                data.Slice(first, second).CopyTo(new Span<T>(_buffer, 0, second));
            }


            Volatile.Write(ref _writePos.Value, Advance(w, toWrite));
            return true;
        }

        // ----------------------------- Consumer API -----------------------------
        /// <summary>
        /// Consumer reads up to destination.Length elements (aligned). Returns number actually read.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(Span<T> destination)
        {
            if (destination.IsEmpty)
            {
                return 0;
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);

            int readable = AvailableToRead(w, r);
            int toRead = Math.Min(destination.Length, readable);
            toRead -= toRead % _alignment;
            if (toRead <= 0)
            {
                return 0;
            }

            int first = Math.Min(toRead, _size - r);
            new ReadOnlySpan<T>(_buffer, r, first).CopyTo(destination.Slice(0, first));
            int second = toRead - first;
            if (second > 0)
            {
                new ReadOnlySpan<T>(_buffer, 0, second).CopyTo(destination.Slice(first, second));
            }


            Volatile.Write(ref _readPos.Value, Advance(r, toRead));
            return toRead;
        }

        /// <summary>
        /// Consumer tries to read exactly <paramref name="count"/> elements; returns false if insufficient data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadExact(Span<T> destination, int count)
        {
            if (count < 0 || count > destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }


            if ((count % _alignment) != 0)
            {
                throw new ArgumentException("Count must align with Alignment.", nameof(count));
            }

            if (count == 0)
            {
                return true;
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);
            int readable = AvailableToRead(w, r);
            if (readable < count)
            {
                return false;
            }


            int first = Math.Min(count, _size - r);
            new ReadOnlySpan<T>(_buffer, r, first).CopyTo(destination.Slice(0, first));
            int second = count - first;
            if (second > 0)
            {
                new ReadOnlySpan<T>(_buffer, 0, second).CopyTo(destination.Slice(first, second));
            }


            Volatile.Write(ref _readPos.Value, Advance(r, count));
            return true;
        }

        /// <summary>
        /// Consumer peeks into buffer without advancing read position. Returns number copied (<= destination.Length).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Peek(Span<T> destination)
        {
            if (destination.IsEmpty)
            {
                return 0;
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);

            int readable = AvailableToRead(w, r);
            int toCopy = Math.Min(destination.Length, readable);
            toCopy -= toCopy % _alignment;
            if (toCopy <= 0)
            {
                return 0;
            }

            int first = Math.Min(toCopy, _size - r);
            new ReadOnlySpan<T>(_buffer, r, first).CopyTo(destination.Slice(0, first));
            int second = toCopy - first;
            if (second > 0)
            {
                new ReadOnlySpan<T>(_buffer, 0, second).CopyTo(destination.Slice(first, second));
            }


            return toCopy;
        }

        /// <summary>
        /// Consumer copies the last <paramref name="count"/> elements without advancing read position.
        /// Useful for sliding-window analytics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyLastExact(Span<T> destination, int count)
        {
            if (count < 0 || count > destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }


            if ((count % _alignment) != 0)
            {
                throw new ArgumentException("Count must align with Alignment.", nameof(count));
            }

            if (count == 0)
            {
                return true;
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);
            int readable = AvailableToRead(w, r);
            if (readable < count)
            {
                return false;
            }


            int start = Sub(w, count); // first element index of the trailing window
            int first = Math.Min(count, _size - start);
            new ReadOnlySpan<T>(_buffer, start, first).CopyTo(destination.Slice(0, first));
            int second = count - first;
            if (second > 0)
            {
                new ReadOnlySpan<T>(_buffer, 0, second).CopyTo(destination.Slice(first, second));
            }


            return true;
        }

        /// <summary>
        /// Consumer skips (advances read position) by up to <paramref name="count"/> elements.
        /// Returns actual skipped elements (aligned).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Skip(int count)
        {
            if (count <= 0)
            {
                return 0;
            }


            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);
            int readable = AvailableToRead(w, r);
            int toSkip = Math.Min(count, readable);
            toSkip -= toSkip % _alignment;
            if (toSkip <= 0)
            {
                return 0;
            }


            Volatile.Write(ref _readPos.Value, Advance(r, toSkip));
            return toSkip;
        }

        /// <summary>
        /// Snapshot zero-copy read: returns up to two spans for the current readable region.
        /// Only call this from the consumer thread and consume immediately.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetSpans(out ReadOnlySpan<T> first, out ReadOnlySpan<T> second)
        {
            int w = Volatile.Read(ref _writePos.Value);
            int r = Volatile.Read(ref _readPos.Value);
            int readable = AvailableToRead(w, r);
            readable -= readable % _alignment;
            if (readable <= 0)
            {
                first = second = ReadOnlySpan<T>.Empty; return;
            }

            if (r <= w)
            {
                first = new ReadOnlySpan<T>(_buffer, r, readable);
                second = ReadOnlySpan<T>.Empty;
            }
            else
            {
                int firstLen = Math.Min(readable, _size - r);
                first = new ReadOnlySpan<T>(_buffer, r, firstLen);
                second = new ReadOnlySpan<T>(_buffer, 0, readable - firstLen);
            }
        }

        /// <summary>Clear buffer (set to empty). Prefer calling when producer/consumer are paused.</summary>
        public void Clear()
        {
            Volatile.Write(ref _writePos.Value, 0);
            Volatile.Write(ref _readPos.Value, 0);
        }

        // ----------------------------- Helpers -----------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AlignCapacity(int requested)
        {
            int a = _alignment;
            int aligned = ((requested + a - 1) / a) * a;
            return aligned;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NextPowerOfTwo(int value)
        {
            if (value <= 2)
            {
                return 2;
            }


            value--;
            value |= value >> 1; value |= value >> 2; value |= value >> 4; value |= value >> 8; value |= value >> 16;
            value++;
            return value < 2 ? 2 : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AvailableToRead(int w, int r) => w >= r ? (w - r) : (w + (_size - r));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AvailableToWrite(int w, int r) => w >= r ? (_size - w + r - 1) : (r - w - 1); // reserve one slot

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Advance(int pos, int n)
        {
            if (_useMask)
            {
                return (pos + n) & _mask;
            }


            pos += n; return (pos >= _size) ? (pos - _size) : pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Sub(int pos, int n)
        {
            if (_useMask)
            {
                return (pos - n) & _mask;
            }


            pos -= n; return (pos < 0) ? (pos + _size) : pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOfTwo(int x) => x > 0 && (x & (x - 1)) == 0;
    }
}
