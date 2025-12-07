using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime.Utilities;
using NUnit.Framework;

namespace Eitan.SherpaONNXUnity.Tests
{
    public class BoundedSampleQueueTests
    {
        [Test]
        public void DropsOldestWhenOverCapacity()
        {
            const int maxSamples = 1024;
            var queue = new BoundedSampleQueue(maxSamples, blockSize: 128);

            var data = Enumerable.Range(0, 2048).Select(i => (float)i).ToArray();
            var dropped = queue.Enqueue(data);

            Assert.Greater(dropped, 0, "Expected the queue to drop old samples when over capacity.");
            Assert.LessOrEqual(queue.QueuedSamples, queue.MaxSamples, "Queue should never exceed capacity.");

            Span<float> buffer = stackalloc float[256];
            int totalRead = 0;
            int expected = data.Length - queue.MaxSamples;

            while (queue.QueuedSamples > 0)
            {
                var read = queue.DequeueInto(buffer);
                if (read == 0)
                {
                    break;
                }

                for (int i = 0; i < read; i++)
                {
                    Assert.AreEqual(expected++, buffer[i], $"Unexpected sample at index {totalRead + i}");
                }

                totalRead += read;
            }

            Assert.AreEqual(maxSamples, totalRead, "Should read exactly the bounded amount of samples.");
            queue.Dispose();
        }

        [Test]
        public async Task ConcurrentStressRemainsBounded()
        {
            const int maxSamples = 4096;
            var queue = new BoundedSampleQueue(maxSamples, blockSize: 256);

            var payload = Enumerable.Range(0, 512).Select(i => (float)(i % 17)).ToArray();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var sw = Stopwatch.StartNew();

            int droppedTotal = 0;
            int maxQueued = 0;
            long consumed = 0;

            var producers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                for (int i = 0; i < 200 && !cts.IsCancellationRequested; i++)
                {
                    var dropped = queue.Enqueue(payload);
                    if (dropped > 0)
                    {
                        Interlocked.Add(ref droppedTotal, dropped);
                    }

                    await Task.Yield();
                }
            }, cts.Token)).ToArray();

            var consumer = Task.Run(async () =>
            {
                var buffer = new float[512];
                while (!cts.IsCancellationRequested && (producers.Any(p => !p.IsCompleted) || queue.QueuedSamples > 0))
                {
                    var current = queue.QueuedSamples;
                    if (current > maxQueued)
                    {
                        maxQueued = current;
                    }

                    var read = queue.DequeueInto(buffer);
                    consumed += read;
                    await Task.Delay(1, cts.Token);
                }
            }, cts.Token);

            await Task.WhenAll(producers);
            sw.Stop();
            await consumer;

            TestContext.WriteLine($"Stress duration: {sw.ElapsedMilliseconds}ms; max queued: {maxQueued}; dropped: {droppedTotal}; remaining: {queue.QueuedSamples}; consumed: {consumed}");

            Assert.LessOrEqual(queue.QueuedSamples, queue.MaxSamples, "Queue grew beyond its configured bound.");
            Assert.LessOrEqual(maxQueued, queue.MaxSamples, "Peak queued samples exceeded capacity.");

            queue.Dispose();
        }
    }
}
