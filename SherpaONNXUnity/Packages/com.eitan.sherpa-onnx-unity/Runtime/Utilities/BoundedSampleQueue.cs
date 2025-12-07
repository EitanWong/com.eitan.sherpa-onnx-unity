namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Thread-safe, bounded PCM queue that keeps only the newest samples.
    /// Drops the oldest data when capacity is exceeded to avoid runaway memory usage under load.
    /// </summary>
    internal sealed class BoundedSampleQueue : IDisposable
    {
        private sealed class Segment
        {
            public float[] Buffer;
            public int Offset;
            public int Length;
        }

        private readonly Queue<Segment> _segments = new Queue<Segment>();
        private readonly object _gate = new object();
        private readonly ArrayPool<float> _pool;
        private readonly int _maxSamples;
        private readonly int _blockSize;

        private int _queuedSamples;
        private bool _disposed;

        public BoundedSampleQueue(int maxSamples, int blockSize = 1024, ArrayPool<float> pool = null)
        {
            if (maxSamples <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSamples), "Max samples must be positive.");
            }

            _maxSamples = maxSamples;
            _blockSize = Math.Max(32, blockSize);
            _pool = pool ?? ArrayPool<float>.Shared;
        }

        public int QueuedSamples => Volatile.Read(ref _queuedSamples);

        public int MaxSamples => _maxSamples;

        /// <summary>
        /// Enqueues samples, dropping the oldest data when capacity is exceeded.
        /// Returns the number of samples discarded.
        /// </summary>
        public int Enqueue(ReadOnlySpan<float> samples)
        {
            if (_disposed || samples.IsEmpty)
            {
                return 0;
            }

            int dropped = 0;

            lock (_gate)
            {
                int offset = 0;
                while (offset < samples.Length)
                {
                    int len = Math.Min(_blockSize, samples.Length - offset);
                    var buffer = _pool.Rent(len);
                    samples.Slice(offset, len).CopyTo(buffer);

                    _segments.Enqueue(new Segment
                    {
                        Buffer = buffer,
                        Offset = 0,
                        Length = len
                    });

                    offset += len;
                }

                _queuedSamples += samples.Length;
                dropped = DropOverflow_NoLock();
            }

            return dropped;
        }

        /// <summary>
        /// Attempts to read up to destination.Length samples into the provided span.
        /// Returns the number of samples written (0 when empty).
        /// </summary>
        public int DequeueInto(Span<float> destination)
        {
            if (_disposed || destination.IsEmpty)
            {
                return 0;
            }

            int written = 0;

            lock (_gate)
            {
                while (written < destination.Length && _segments.Count > 0)
                {
                    var seg = _segments.Peek();
                    int toCopy = Math.Min(seg.Length, destination.Length - written);

                    seg.Buffer.AsSpan(seg.Offset, toCopy).CopyTo(destination.Slice(written, toCopy));

                    written += toCopy;
                    seg.Offset += toCopy;
                    seg.Length -= toCopy;
                    _queuedSamples -= toCopy;

                    if (seg.Length == 0)
                    {
                        _segments.Dequeue();
                        _pool.Return(seg.Buffer);
                    }
                }
            }

            return written;
        }

        public void Clear()
        {
            lock (_gate)
            {
                while (_segments.Count > 0)
                {
                    var seg = _segments.Dequeue();
                    _pool.Return(seg.Buffer);
                }

                _queuedSamples = 0;
            }
        }

        private int DropOverflow_NoLock()
        {
            int dropped = 0;

            while (_queuedSamples > _maxSamples && _segments.Count > 0)
            {
                var seg = _segments.Dequeue();
                dropped += seg.Length;
                _queuedSamples -= seg.Length;
                _pool.Return(seg.Buffer);
            }

            return dropped;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Clear();
            _disposed = true;
        }
    }
}
