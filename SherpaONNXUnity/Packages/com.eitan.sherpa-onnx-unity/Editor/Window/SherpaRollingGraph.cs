#if UNITY_EDITOR
// Rolling graph with dual backend:
// - Burst/DOTS path when BURST_PRESENT is defined (package installed)
// - Managed fallback path when Burst/Math not present
// The public surface stays identical; caller does not care which backend is active.

namespace Eitan.SherpaONNXUnity.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

#if BURST_PRESENT
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
#endif

    internal enum GraphSeriesType { Line, Area, Bar, Points }
    internal enum GraphScaleMode { Unified, PerTrack, Fixed }

    internal struct GraphTrackConfig
    {
        public string Name;
        public GraphSeriesType SeriesType;
        public Color Color;
        public Color FillColor;
        public bool Visible;
        public float Smoothing;
        public float RangePadding;
        public bool UseLogScale;
        public bool UseFixedRange;
        public Vector2 FixedRange;
        public float Baseline;
    }

    internal sealed class SherpaRollingGraph : IDisposable
    {
        private const float MinRangeSpan = 0.001f;
        private const float GraphPadding = 6f;
        private const float GraphContentPadding = 2f;
        private const float RangeSmoothing = 0.18f;
        private const float RangeMaxExpansionFactor = 6f;
        private static readonly Color GraphBackground = new Color(0.12f, 0.12f, 0.12f, 1f);

#if BURST_PRESENT
        private static readonly bool s_HighPerformanceAvailable = DetectHighPerformanceBackend();

        private static bool DetectHighPerformanceBackend()
        {
            try
            {
                return Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst") != null &&
                       Type.GetType("Unity.Mathematics.math, Unity.Mathematics") != null;
            }
            catch
            {
                return false;
            }
        }
#endif

        private interface IBackend : IDisposable
        {
            void SetTrackConfig(int trackIndex, in GraphTrackConfig config);
            void SetIncoming(int trackIndex, float value);
            void SetVisibleSamples(int count);
            void Schedule();
            void Complete();
            void Draw(Rect rect, GraphScaleMode scaleMode, bool logScaleOverride, Vector2 fixedRange);
        }

        private readonly IBackend _backend;

        public SherpaRollingGraph(int trackCount, int capacity, int visibleSamples)
        {
            IBackend selectedBackend = null;

#if BURST_PRESENT
            if (s_HighPerformanceAvailable)
            {
                try
                {
                    selectedBackend = new BurstBackend(trackCount, capacity, visibleSamples);
                }
                catch (Exception)
                {
                    // Burst path failed to init, fall back to managed.
                    selectedBackend = null;
                }
            }
#endif

            _backend = selectedBackend ?? new ManagedBackend(trackCount, capacity, visibleSamples);
        }

        public void Dispose()
        {
            _backend?.Dispose();
        }

        public void SetTrackConfig(int trackIndex, in GraphTrackConfig config) => _backend.SetTrackConfig(trackIndex, config);
        public void SetIncoming(int trackIndex, float value) => _backend.SetIncoming(trackIndex, value);
        public void SetVisibleSamples(int count) => _backend.SetVisibleSamples(count);
        public void Schedule() => _backend.Schedule();
        public void Complete() => _backend.Complete();
        public void Draw(Rect rect, GraphScaleMode scaleMode, bool logScaleOverride, Vector2 fixedRange) => _backend.Draw(rect, scaleMode, logScaleOverride, fixedRange);

        private static bool TryBuildGraphRects(Rect hostRect, out Rect graphRect, out Rect contentRect)
        {
            graphRect = new Rect(
                Mathf.Floor(hostRect.x + GraphPadding),
                Mathf.Floor(hostRect.y + GraphPadding),
                Mathf.Max(2f, hostRect.width - GraphPadding * 2f),
                Mathf.Max(2f, hostRect.height - GraphPadding * 2f));

            if (graphRect.width < 4f || graphRect.height < 4f)
            {
                contentRect = default;
                return false;
            }

            contentRect = new Rect(
                GraphContentPadding,
                GraphContentPadding,
                Mathf.Max(1f, graphRect.width - GraphContentPadding * 2f),
                Mathf.Max(1f, graphRect.height - GraphContentPadding * 2f));

            return contentRect.width >= 2f && contentRect.height >= 2f;
        }

#if BURST_PRESENT
        private sealed class BurstBackend : IBackend
        {
            private readonly int _trackCount;
            private readonly int _capacity;
            private int _visibleSamples;

            private readonly GraphTrackConfig[] _configs;
            private readonly Vector3[] _pointCache;
            private readonly Vector3[] _areaCache;

            private NativeArray<float> _incoming;
            private NativeArray<float> _frameSamples;
            private NativeArray<float> _ring;
            private NativeArray<float> _smoothed;
            private NativeArray<int> _writeHead;
            private NativeArray<int> _counts;
            private NativeArray<int> _lastWriteIndex;
            private NativeArray<float2> _range;
            private NativeArray<float> _smoothing;
            private NativeArray<float> _rangePadding;
            private NativeArray<float2> _fixedRange;
            private NativeArray<byte> _useFixedRange;
            private NativeArray<float> _baseline;

            private JobHandle _handle;
            private bool _rangeDirty;

            public BurstBackend(int trackCount, int capacity, int visibleSamples)
            {
                _trackCount = math.max(1, trackCount);
                _capacity = math.max(8, capacity);
                _visibleSamples = math.clamp(visibleSamples, 2, _capacity);

                _configs = new GraphTrackConfig[_trackCount];
                _pointCache = new Vector3[_capacity];
                _areaCache = new Vector3[_capacity * 2];

                _incoming = new NativeArray<float>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _frameSamples = new NativeArray<float>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _ring = new NativeArray<float>(_trackCount * _capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _smoothed = new NativeArray<float>(_trackCount * _capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _writeHead = new NativeArray<int>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _counts = new NativeArray<int>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _lastWriteIndex = new NativeArray<int>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _range = new NativeArray<float2>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _smoothing = new NativeArray<float>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _rangePadding = new NativeArray<float>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _fixedRange = new NativeArray<float2>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _useFixedRange = new NativeArray<byte>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _baseline = new NativeArray<float>(_trackCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

                for (int i = 0; i < _trackCount; i++)
                {
                    _lastWriteIndex[i] = -1;
                    _range[i] = new float2(0f, 1f);
                    _smoothing[i] = 0.35f;
                    _rangePadding[i] = 0.05f;
                    _fixedRange[i] = new float2(0f, 1f);
                    _useFixedRange[i] = 0;
                    _baseline[i] = 0f;
                }

                _rangeDirty = true;
            }

            public void Dispose()
            {
                _handle.Complete();

                DisposeNative(ref _incoming);
                DisposeNative(ref _frameSamples);
                DisposeNative(ref _ring);
                DisposeNative(ref _smoothed);
                DisposeNative(ref _writeHead);
                DisposeNative(ref _counts);
                DisposeNative(ref _lastWriteIndex);
                DisposeNative(ref _range);
                DisposeNative(ref _smoothing);
                DisposeNative(ref _rangePadding);
                DisposeNative(ref _fixedRange);
                DisposeNative(ref _useFixedRange);
                DisposeNative(ref _baseline);
            }

            private static void DisposeNative<T>(ref NativeArray<T> array) where T : struct
            {
                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }

            public void SetTrackConfig(int trackIndex, in GraphTrackConfig config)
            {
                if ((uint)trackIndex >= (uint)_trackCount)
                {
                    return;
                }

                _configs[trackIndex] = config;
                _smoothing[trackIndex] = math.clamp(config.Smoothing, 0f, 1f);
                _rangePadding[trackIndex] = math.max(0f, config.RangePadding);
                _useFixedRange[trackIndex] = config.UseFixedRange ? (byte)1 : (byte)0;
                _fixedRange[trackIndex] = new float2(config.FixedRange.x, math.max(config.FixedRange.y, config.FixedRange.x + MinRangeSpan));
                _baseline[trackIndex] = config.Baseline;
                _rangeDirty = true;
            }

            public void SetIncoming(int trackIndex, float value)
            {
                if ((uint)trackIndex >= (uint)_trackCount)
                {
                    return;
                }

                _incoming[trackIndex] = value;
            }

            public void SetVisibleSamples(int count)
            {
                _visibleSamples = math.clamp(count, 2, _capacity);
                _rangeDirty = true;
            }

            public void Schedule()
            {
                var sampling = new SamplingJob
                {
                    Input = _incoming,
                    FrameSamples = _frameSamples
                }.Schedule();

                var rolling = new RollingWindowJob
                {
                    FrameSamples = _frameSamples,
                    Ring = _ring,
                    WriteHead = _writeHead,
                    Counts = _counts,
                    LastWriteIndex = _lastWriteIndex,
                    Capacity = _capacity
                }.Schedule(sampling);

                var smoothing = new SmoothingJob
                {
                    Ring = _ring,
                    Smoothed = _smoothed,
                    LastWriteIndex = _lastWriteIndex,
                    Counts = _counts,
                    Capacity = _capacity,
                    Smoothing = _smoothing,
                    Baseline = _baseline
                }.Schedule(rolling);

                var autoRange = new AutoRangeJob
                {
                    Buffer = _smoothed,
                    Counts = _counts,
                    WriteHead = _writeHead,
                    Range = _range,
                    Capacity = _capacity,
                    VisibleSamples = _visibleSamples,
                    RangePadding = _rangePadding,
                    UseFixedRange = _useFixedRange,
                    FixedRange = _fixedRange
                }.Schedule(_trackCount, 1, smoothing);

                _handle = autoRange;
                _rangeDirty = false;
            }

            public void Complete()
            {
                _handle.Complete();
            }

            private void RefreshRangeIfDirty()
            {
                if (!_rangeDirty)
                {
                    return;
                }

                var handle = new AutoRangeJob
                {
                    Buffer = _smoothed,
                    Counts = _counts,
                    WriteHead = _writeHead,
                    Range = _range,
                    Capacity = _capacity,
                    VisibleSamples = _visibleSamples,
                    RangePadding = _rangePadding,
                    UseFixedRange = _useFixedRange,
                    FixedRange = _fixedRange
                }.Schedule(_trackCount, 1);

                handle.Complete();
                _handle = handle;
                _rangeDirty = false;
            }

            public void Draw(Rect rect, GraphScaleMode scaleMode, bool logScaleOverride, Vector2 fixedRange)
            {
                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                if (!TryBuildGraphRects(rect, out var graphRect, out var contentRect))
                {
                    return;
                }

                Complete();
                RefreshRangeIfDirty();

                float unifiedMin = float.MaxValue;
                float unifiedMax = float.MinValue;

                for (int i = 0; i < _trackCount; i++)
                {
                    if (!_configs[i].Visible)
                    {
                        continue;
                    }

                    var range = _range[i];
                    unifiedMin = math.min(unifiedMin, range.x);
                    unifiedMax = math.max(unifiedMax, range.y);
                }

                if (unifiedMin == float.MaxValue || unifiedMax == float.MinValue)
                {
                    return;
                }

                if (scaleMode == GraphScaleMode.Fixed)
                {
                    unifiedMin = fixedRange.x;
                    unifiedMax = math.max(fixedRange.y, unifiedMin + MinRangeSpan);
                }
                else if (unifiedMax - unifiedMin < MinRangeSpan)
                {
                    unifiedMax = unifiedMin + MinRangeSpan;
                }

                EditorGUI.DrawRect(graphRect, GraphBackground);

                GUI.BeginGroup(graphRect);
                Handles.BeginGUI();
                RenderSeries(GraphSeriesType.Bar, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                RenderSeries(GraphSeriesType.Area, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                RenderSeries(GraphSeriesType.Line, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                RenderSeries(GraphSeriesType.Points, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                Handles.color = Color.white;
                Handles.EndGUI();
                GUI.EndGroup();
            }

            private void RenderSeries(GraphSeriesType targetType, Rect contentRect, GraphScaleMode scaleMode, bool logScaleOverride, float unifiedMin, float unifiedMax)
            {
                for (int track = 0; track < _trackCount; track++)
                {
                    ref var config = ref _configs[track];
                    if (!config.Visible || config.SeriesType != targetType)
                    {
                        continue;
                    }

                    RenderTrack(contentRect, track, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                }
            }

            private void RenderTrack(Rect contentRect, int track, GraphScaleMode scaleMode, bool logScaleOverride, float unifiedMin, float unifiedMax)
            {
                int count = math.min(_visibleSamples, _counts[track]);
                if (count == 0)
                {
                    return;
                }

                ref var config = ref _configs[track];

                if ((config.SeriesType == GraphSeriesType.Line || config.SeriesType == GraphSeriesType.Area) && count < 2)
                {
                    return;
                }

                float2 range = _range[track];
                if (scaleMode == GraphScaleMode.Unified)
                {
                    range.x = unifiedMin;
                    range.y = unifiedMax;
                }
                else if (scaleMode == GraphScaleMode.Fixed && _useFixedRange[track] == 0)
                {
                    range.x = unifiedMin;
                    range.y = unifiedMax;
                }

                if (_useFixedRange[track] == 1 && scaleMode == GraphScaleMode.PerTrack)
                {
                    range = _fixedRange[track];
                }

                float min = range.x;
                float max = range.y;
                if (max - min < MinRangeSpan)
                {
                    max = min + MinRangeSpan;
                }

                bool useLog = logScaleOverride || config.UseLogScale;

                var invCount = count > 1 ? 1f / (count - 1) : 1f;
                var width = contentRect.width - 2f;
                var height = contentRect.height - 2f;
                var x0 = contentRect.x + 1f;
                var yBase = contentRect.yMax - 1f;

                float baselineNormalized = math.clamp(math.unlerp(min, max, config.Baseline), 0f, 1f);
                float baselineY = math.clamp(yBase - baselineNormalized * height, contentRect.yMin, contentRect.yMax);

                int start = _writeHead[track] - count;
                while (start < 0)
                {
                    start += _capacity;
                }

                int offset = track * _capacity;

                for (int i = 0; i < count; i++)
                {
                    int idx = offset + ((start + i) % _capacity);
                    float sample = _smoothed[idx];
                    float normalized = useLog ? NormalizeLog(sample, min, max) : math.unlerp(min, max, sample);
                    normalized = math.clamp(normalized, 0f, 1f);

                    float px = x0 + i * invCount * width;
                    float py = yBase - normalized * height;

                    _pointCache[i].x = px;
                    _pointCache[i].y = py;
                    _pointCache[i].z = 0f;
                }

                Handles.color = config.Color;

                switch (config.SeriesType)
                {
                    case GraphSeriesType.Line:
                        Handles.DrawAAPolyLine(2f, count, _pointCache);
                        break;

                    case GraphSeriesType.Points:
                        var size = math.max(2f, Mathf.Round(height * 0.01f));
                        for (int i = 0; i < count; i++)
                        {
                            Handles.DrawSolidDisc(_pointCache[i], Vector3.forward, size);
                        }
                        break;

                    case GraphSeriesType.Bar:
                        DrawBars(ref config, contentRect, count, width, invCount, baselineY);
                        break;

                    case GraphSeriesType.Area:
                        DrawArea(ref config, contentRect, count, baselineY);
                        break;
                }
            }

            private void DrawBars(ref GraphTrackConfig config, Rect contentRect, int count, float width, float invCount, float baselineY)
            {
                var barWidth = math.max(1f, width * invCount * 0.85f);
                var halfWidth = barWidth * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    var center = _pointCache[i];
                    var left = Mathf.Floor(center.x - halfWidth) + 0.5f;
                    var right = left + barWidth;

                    left = Mathf.Max(contentRect.xMin, left);
                    right = Mathf.Min(contentRect.xMax, right);

                    var top = Mathf.Clamp(center.y, contentRect.yMin, contentRect.yMax);
                    var bottom = Mathf.Clamp(baselineY, contentRect.yMin, contentRect.yMax);

                    if (top > bottom)
                    {
                        var temp = top;
                        top = bottom;
                        bottom = temp;
                    }

                    if (right <= left || bottom <= top)
                    {
                        continue;
                    }

                    var barRect = new Rect(left, top, right - left, bottom - top);
                    EditorGUI.DrawRect(barRect, config.Color);
                }
            }

            private void DrawArea(ref GraphTrackConfig config, Rect contentRect, int count, float baselineY)
            {
                int vertexCount = count * 2;
                if (vertexCount > _areaCache.Length)
                {
                    return;
                }

                float clampedBaseline = Mathf.Clamp(baselineY, contentRect.yMin, contentRect.yMax);

                for (int i = 0; i < count; i++)
                {
                    _areaCache[i] = _pointCache[i];
                }

                for (int i = 0; i < count; i++)
                {
                    int idx = vertexCount - 1 - i;
                    _areaCache[idx].x = Mathf.Clamp(_pointCache[i].x, contentRect.xMin, contentRect.xMax);
                    _areaCache[idx].y = clampedBaseline;
                    _areaCache[idx].z = 0f;
                }

                Handles.color = config.FillColor;
                Handles.DrawAAConvexPolygon(_areaCache);

                Handles.color = config.Color;
                Handles.DrawAAPolyLine(2f, count, _pointCache);
            }

            private static float NormalizeLog(float value, float min, float max)
            {
                const float Epsilon = 1e-5f;

                float clampedMin = math.max(min, Epsilon);
                float clampedMax = math.max(max, clampedMin + Epsilon);
                float clampedValue = math.clamp(value, clampedMin, clampedMax);

                float logMin = math.log10(clampedMin);
                float logMax = math.log10(clampedMax);
                float logValue = math.log10(clampedValue);

                return math.unlerp(logMin, logMax, logValue);
            }

            [BurstCompile]
            private struct SamplingJob : IJob
            {
                [ReadOnly] public NativeArray<float> Input;
                public NativeArray<float> FrameSamples;

                public void Execute()
                {
                    for (int i = 0; i < FrameSamples.Length; i++)
                    {
                        FrameSamples[i] = Input[i];
                    }
                }
            }

            [BurstCompile]
            private struct RollingWindowJob : IJob
            {
                [ReadOnly] public NativeArray<float> FrameSamples;
                public NativeArray<float> Ring;
                public NativeArray<int> WriteHead;
                public NativeArray<int> Counts;
                public NativeArray<int> LastWriteIndex;
                public int Capacity;

                public void Execute()
                {
                    var trackCount = WriteHead.Length;
                    for (int track = 0; track < trackCount; track++)
                    {
                        int head = WriteHead[track];
                        int count = Counts[track];

                        int writeIndex = track * Capacity + head;
                        Ring[writeIndex] = FrameSamples[track];
                        LastWriteIndex[track] = writeIndex;

                        head++;
                        if (head == Capacity)
                        {
                            head = 0;
                        }

                        if (count < Capacity)
                        {
                            count++;
                        }

                        WriteHead[track] = head;
                        Counts[track] = count;
                    }
                }
            }

        [BurstCompile]
        private struct SmoothingJob : IJob
        {
            [ReadOnly] public NativeArray<float> Ring;
            [ReadOnly] public NativeArray<int> LastWriteIndex;
            [ReadOnly] public NativeArray<int> Counts;
            [ReadOnly] public NativeArray<float> Smoothing;
            [ReadOnly] public NativeArray<float> Baseline;
            public NativeArray<float> Smoothed;
            public int Capacity;

            public void Execute()
            {
                var trackCount = Smoothing.Length;
                for (int track = 0; track < trackCount; track++)
                {
                    int lastIndex = LastWriteIndex[track];
                    if (lastIndex < 0)
                    {
                        continue;
                    }

                    float value = Ring[lastIndex];
                    float factor = math.clamp(Smoothing[track], 0f, 1f);
                    int count = Counts[track];

                    float prev;
                    if (count > 1)
                    {
                        int prevIndex = lastIndex - 1;
                        int trackStart = track * Capacity;
                        if (prevIndex < trackStart)
                        {
                            prevIndex = trackStart + Capacity - 1;
                        }
                        prev = Smoothed[prevIndex];
                    }
                    else
                    {
                        prev = Baseline[track];
                    }

                    value = math.lerp(prev, value, 1f - factor);
                    Smoothed[lastIndex] = value;
                }
            }
            [BurstCompile]
            private struct AutoRangeJob : IJobParallelFor
            {
                [ReadOnly] public NativeArray<float> Buffer;
                [ReadOnly] public NativeArray<int> Counts;
                [ReadOnly] public NativeArray<int> WriteHead;
                [ReadOnly] public NativeArray<float> RangePadding;
                [ReadOnly] public NativeArray<byte> UseFixedRange;
                [ReadOnly] public NativeArray<float2> FixedRange;
                public NativeArray<float2> Range;
                public int Capacity;
                public int VisibleSamples;

                public void Execute(int track)
                {
                    if (UseFixedRange[track] == 1)
                    {
                        Range[track] = FixedRange[track];
                        return;
                    }

                    int count = math.min(Counts[track], VisibleSamples);
                    if (count <= 0)
                    {
                        Range[track] = new float2(0f, 1f);
                        return;
                    }

                    int head = WriteHead[track];
                    int start = head - count;
                    while (start < 0)
                    {
                        start += Capacity;
                    }

                    int offset = track * Capacity;

                    float min = float.MaxValue;
                    float max = float.MinValue;

                    for (int i = 0; i < count; i++)
                    {
                        int idx = offset + ((start + i) % Capacity);
                        float v = Buffer[idx];
                        min = math.min(min, v);
                        max = math.max(max, v);
                    }

                    if (max - min < MinRangeSpan)
                    {
                        max = min + MinRangeSpan;
                    }

                    float pad = (max - min) * RangePadding[track];
                    min -= pad;
                    max += pad;

                    var prev = Range[track];
                    float prevSpan = math.max(prev.y - prev.x, MinRangeSpan);
                    float targetSpan = max - min;
                    float maxSpan = prevSpan * RangeMaxExpansionFactor;
                    if (targetSpan > maxSpan)
                    {
                        float center = (max + min) * 0.5f;
                        targetSpan = maxSpan;
                        min = center - targetSpan * 0.5f;
                        max = center + targetSpan * 0.5f;
                    }

                    Range[track] = new float2(
                        math.lerp(prev.x, min, RangeSmoothing),
                        math.lerp(prev.y, max, RangeSmoothing));
                }
            }
        }
#endif

        private sealed class ManagedBackend : IBackend
        {
            private readonly int _trackCount;
            private readonly int _capacity;
            private int _visibleSamples;

            private readonly GraphTrackConfig[] _configs;
            private readonly float[] _incoming;
            private readonly float[] _ring;
            private readonly float[] _smoothed;
            private readonly int[] _writeHead;
            private readonly int[] _counts;
            private readonly int[] _lastWriteIndex;
            private readonly Vector2[] _range;
            private readonly float[] _smoothing;
            private readonly float[] _rangePadding;
            private readonly Vector2[] _fixedRange;
            private readonly bool[] _useFixedRange;
            private readonly float[] _baseline;
            private readonly Vector3[] _pointCache;
            private readonly Vector3[] _areaCache;
            private bool _rangeDirty;

            public ManagedBackend(int trackCount, int capacity, int visibleSamples)
            {
                _trackCount = Mathf.Max(1, trackCount);
                _capacity = Mathf.Max(8, capacity);
                _visibleSamples = Mathf.Clamp(visibleSamples, 2, _capacity);

                _configs = new GraphTrackConfig[_trackCount];
                _incoming = new float[_trackCount];
                _ring = new float[_trackCount * _capacity];
                _smoothed = new float[_trackCount * _capacity];
                _writeHead = new int[_trackCount];
                _counts = new int[_trackCount];
                _lastWriteIndex = new int[_trackCount];
                _range = new Vector2[_trackCount];
                _smoothing = new float[_trackCount];
                _rangePadding = new float[_trackCount];
                _fixedRange = new Vector2[_trackCount];
                _useFixedRange = new bool[_trackCount];
                _baseline = new float[_trackCount];
                _pointCache = new Vector3[_capacity];
                _areaCache = new Vector3[_capacity * 2];

                for (int i = 0; i < _trackCount; i++)
                {
                    _lastWriteIndex[i] = -1;
                    _range[i] = new Vector2(0f, 1f);
                    _smoothing[i] = 0.35f;
                    _rangePadding[i] = 0.05f;
                    _fixedRange[i] = new Vector2(0f, 1f);
                    _useFixedRange[i] = false;
                    _baseline[i] = 0f;
                }
                _rangeDirty = true;
            }

            public void Dispose()
            {
                // managed only; nothing to dispose
            }

            public void SetTrackConfig(int trackIndex, in GraphTrackConfig config)
            {
                if ((uint)trackIndex >= (uint)_trackCount)
                {
                    return;
                }

                _configs[trackIndex] = config;
                _smoothing[trackIndex] = Mathf.Clamp01(config.Smoothing);
                _rangePadding[trackIndex] = Mathf.Max(0f, config.RangePadding);
                _useFixedRange[trackIndex] = config.UseFixedRange;
                _fixedRange[trackIndex] = new Vector2(config.FixedRange.x, Mathf.Max(config.FixedRange.y, config.FixedRange.x + MinRangeSpan));
                _baseline[trackIndex] = config.Baseline;
                _rangeDirty = true;
            }

            public void SetIncoming(int trackIndex, float value)
            {
                if ((uint)trackIndex >= (uint)_trackCount)
                {
                    return;
                }

                _incoming[trackIndex] = value;
            }

            public void SetVisibleSamples(int count)
            {
                _visibleSamples = Mathf.Clamp(count, 2, _capacity);
                _rangeDirty = true;
            }

            public void Schedule()
            {
                // Rolling - 不变
                for (int track = 0; track < _trackCount; track++)
                {
                    int head = _writeHead[track];
                    int count = _counts[track];

                    int writeIndex = track * _capacity + head;
                    _ring[writeIndex] = _incoming[track];
                    _lastWriteIndex[track] = writeIndex;

                    head++;
                    if (head == _capacity)
                    {
                        head = 0;
                    }

                    if (count < _capacity)
                    {
                        count++;
                    }

                    _writeHead[track] = head;
                    _counts[track] = count;
                }

                for (int track = 0; track < _trackCount; track++)
                {
                    int lastIndex = _lastWriteIndex[track];
                    if (lastIndex < 0)
                    {
                        continue;
                    }

                    float value = _ring[lastIndex];
                    float factor = Mathf.Clamp01(_smoothing[track]);
                    int count = _counts[track];

                    float prev;
                    if (count > 1)
                    {
                        int prevIndex = lastIndex - 1;
                        int trackStart = track * _capacity;
                        if (prevIndex < trackStart)
                        {
                            prevIndex = trackStart + _capacity - 1;
                        }
                        prev = _smoothed[prevIndex];
                    }
                    else
                    {
                        prev = _baseline[track];
                    }

                    value = Mathf.Lerp(prev, value, 1f - factor);
                    _smoothed[lastIndex] = value;
                }

                UpdateRange();
                _rangeDirty = false;
            }
            public void Complete()
            {
                // no-op for managed path
            }

            private void UpdateRange()
            {
                for (int track = 0; track < _trackCount; track++)
                {
                    if (_useFixedRange[track])
                    {
                        _range[track] = _fixedRange[track];
                        continue;
                    }

                    int count = Mathf.Min(_counts[track], _visibleSamples);
                    if (count <= 0)
                    {
                        _range[track] = new Vector2(0f, 1f);
                        continue;
                    }

                    int head = _writeHead[track];
                    int start = head - count;
                    while (start < 0)
                    {
                        start += _capacity;
                    }

                    int offset = track * _capacity;

                    float min = float.MaxValue;
                    float max = float.MinValue;

                    for (int i = 0; i < count; i++)
                    {
                        int idx = offset + ((start + i) % _capacity);
                        float v = _smoothed[idx];
                        if (v < min)
                        {
                            min = v;
                        }


                        if (v > max)
                        {
                            max = v;
                        }

                    }

                    if (max - min < MinRangeSpan)
                    {
                        max = min + MinRangeSpan;
                    }

                    float pad = (max - min) * _rangePadding[track];
                    min -= pad;
                    max += pad;

                    var prev = _range[track];
                    float prevSpan = Mathf.Max(prev.y - prev.x, MinRangeSpan);
                    float targetSpan = max - min;
                    float maxSpan = prevSpan * RangeMaxExpansionFactor;
                    if (targetSpan > maxSpan)
                    {
                        float center = (max + min) * 0.5f;
                        targetSpan = maxSpan;
                        min = center - targetSpan * 0.5f;
                        max = center + targetSpan * 0.5f;
                    }

                    _range[track] = new Vector2(
                        Mathf.Lerp(prev.x, min, RangeSmoothing),
                        Mathf.Lerp(prev.y, max, RangeSmoothing));
                }
            }

            public void Draw(Rect rect, GraphScaleMode scaleMode, bool logScaleOverride, Vector2 fixedRange)
            {
                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                if (!TryBuildGraphRects(rect, out var graphRect, out var contentRect))
                {
                    return;
                }

                if (_rangeDirty)
                {
                    UpdateRange();
                    _rangeDirty = false;
                }

                float unifiedMin = float.MaxValue;
                float unifiedMax = float.MinValue;

                for (int i = 0; i < _trackCount; i++)
                {
                    if (!_configs[i].Visible)
                    {
                        continue;
                    }

                    var range = _range[i];
                    if (range.x < unifiedMin)
                    {
                        unifiedMin = range.x;
                    }


                    if (range.y > unifiedMax)
                    {
                        unifiedMax = range.y;
                    }

                }

                if (unifiedMin == float.MaxValue || unifiedMax == float.MinValue)
                {
                    return;
                }

                if (scaleMode == GraphScaleMode.Fixed)
                {
                    unifiedMin = fixedRange.x;
                    unifiedMax = Mathf.Max(fixedRange.y, unifiedMin + MinRangeSpan);
                }
                else if (unifiedMax - unifiedMin < MinRangeSpan)
                {
                    unifiedMax = unifiedMin + MinRangeSpan;
                }

                EditorGUI.DrawRect(graphRect, GraphBackground);

                GUI.BeginGroup(graphRect);
                Handles.BeginGUI();
                RenderSeries(GraphSeriesType.Bar, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                RenderSeries(GraphSeriesType.Area, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                RenderSeries(GraphSeriesType.Line, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                RenderSeries(GraphSeriesType.Points, contentRect, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                Handles.color = Color.white;
                Handles.EndGUI();
                GUI.EndGroup();
            }

            private void RenderSeries(GraphSeriesType targetType, Rect contentRect, GraphScaleMode scaleMode, bool logScaleOverride, float unifiedMin, float unifiedMax)
            {
                for (int track = 0; track < _trackCount; track++)
                {
                    ref var config = ref _configs[track];
                    if (!config.Visible || config.SeriesType != targetType)
                    {
                        continue;
                    }

                    RenderTrack(contentRect, track, scaleMode, logScaleOverride, unifiedMin, unifiedMax);
                }
            }

            private void RenderTrack(Rect contentRect, int track, GraphScaleMode scaleMode, bool logScaleOverride, float unifiedMin, float unifiedMax)
            {
                int count = Mathf.Min(_visibleSamples, _counts[track]);
                if (count == 0)
                {
                    return;
                }

                ref var config = ref _configs[track];

                if ((config.SeriesType == GraphSeriesType.Line || config.SeriesType == GraphSeriesType.Area) && count < 2)
                {
                    return;
                }

                Vector2 range = _range[track];
                if (scaleMode == GraphScaleMode.Unified)
                {
                    range.x = unifiedMin;
                    range.y = unifiedMax;
                }
                else if (scaleMode == GraphScaleMode.Fixed && !_useFixedRange[track])
                {
                    range.x = unifiedMin;
                    range.y = unifiedMax;
                }

                if (_useFixedRange[track] && scaleMode == GraphScaleMode.PerTrack)
                {
                    range = _fixedRange[track];
                }

                float min = range.x;
                float max = range.y;
                if (max - min < MinRangeSpan)
                {
                    max = min + MinRangeSpan;
                }

                bool useLog = logScaleOverride || config.UseLogScale;

                var invCount = count > 1 ? 1f / (count - 1) : 1f;
                var width = contentRect.width - 2f;
                var height = contentRect.height - 2f;
                var x0 = contentRect.x + 1f;
                var yBase = contentRect.yMax - 1f;

                float baselineNormalized = Mathf.Clamp01(Mathf.InverseLerp(min, max, config.Baseline));
                float baselineY = Mathf.Clamp(yBase - baselineNormalized * height, contentRect.yMin, contentRect.yMax);

                int start = _writeHead[track] - count;
                while (start < 0)
                {
                    start += _capacity;
                }

                int offset = track * _capacity;

                for (int i = 0; i < count; i++)
                {
                    int idx = offset + ((start + i) % _capacity);
                    float sample = _smoothed[idx];
                    float normalized = useLog ? NormalizeLog(sample, min, max) : Mathf.InverseLerp(min, max, sample);
                    normalized = Mathf.Clamp01(normalized);

                    float px = x0 + i * invCount * width;
                    float py = yBase - normalized * height;

                    _pointCache[i].x = px;
                    _pointCache[i].y = py;
                    _pointCache[i].z = 0f;
                }

                Handles.color = config.Color;

                switch (config.SeriesType)
                {
                    case GraphSeriesType.Line:
                        Handles.DrawAAPolyLine(2f, count, _pointCache);
                        break;

                    case GraphSeriesType.Points:
                        var size = Mathf.Max(2f, Mathf.Round(height * 0.01f));
                        for (int i = 0; i < count; i++)
                        {
                            Handles.DrawSolidDisc(_pointCache[i], Vector3.forward, size);
                        }
                        break;

                    case GraphSeriesType.Bar:
                        DrawBars(ref config, contentRect, count, width, invCount, baselineY);
                        break;

                    case GraphSeriesType.Area:
                        DrawArea(ref config, contentRect, count, baselineY);
                        break;
                }
            }

            private void DrawBars(ref GraphTrackConfig config, Rect contentRect, int count, float width, float invCount, float baselineY)
            {
                var barWidth = Mathf.Max(1f, width * invCount * 0.85f);
                var halfWidth = barWidth * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    var center = _pointCache[i];
                    var left = Mathf.Floor(center.x - halfWidth) + 0.5f;
                    var right = left + barWidth;

                    left = Mathf.Max(contentRect.xMin, left);
                    right = Mathf.Min(contentRect.xMax, right);

                    var top = Mathf.Clamp(center.y, contentRect.yMin, contentRect.yMax);
                    var bottom = Mathf.Clamp(baselineY, contentRect.yMin, contentRect.yMax);

                    if (top > bottom)
                    {
                        var temp = top;
                        top = bottom;
                        bottom = temp;
                    }

                    if (right <= left || bottom <= top)
                    {
                        continue;
                    }

                    var barRect = new Rect(left, top, right - left, bottom - top);
                    EditorGUI.DrawRect(barRect, config.Color);
                }
            }

            private void DrawArea(ref GraphTrackConfig config, Rect contentRect, int count, float baselineY)
            {
                int vertexCount = count * 2;
                if (vertexCount > _areaCache.Length)
                {
                    return;
                }

                float clampedBaseline = Mathf.Clamp(baselineY, contentRect.yMin, contentRect.yMax);

                for (int i = 0; i < count; i++)
                {
                    _areaCache[i] = _pointCache[i];
                }

                for (int i = 0; i < count; i++)
                {
                    int idx = vertexCount - 1 - i;
                    _areaCache[idx].x = Mathf.Clamp(_pointCache[i].x, contentRect.xMin, contentRect.xMax);
                    _areaCache[idx].y = clampedBaseline;
                    _areaCache[idx].z = 0f;
                }

                Handles.color = config.FillColor;
                Handles.DrawAAConvexPolygon(_areaCache);
                Handles.color = config.Color;
                Handles.DrawAAPolyLine(2f, count, _pointCache);
            }

            private static float NormalizeLog(float value, float min, float max)
            {
                const float Epsilon = 1e-5f;

                float clampedMin = Mathf.Max(min, Epsilon);
                float clampedMax = Mathf.Max(max, clampedMin + Epsilon);
                float clampedValue = Mathf.Clamp(value, clampedMin, clampedMax);

                float logMin = Mathf.Log10(clampedMin);
                float logMax = Mathf.Log10(clampedMax);
                float logValue = Mathf.Log10(clampedValue);

                return Mathf.InverseLerp(logMin, logMax, logValue);
            }
        }
    }
}

#endif
