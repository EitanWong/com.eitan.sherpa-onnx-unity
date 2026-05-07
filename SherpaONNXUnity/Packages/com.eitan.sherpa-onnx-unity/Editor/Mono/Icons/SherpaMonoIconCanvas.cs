namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Icons
{
    using UnityEngine;

    internal sealed class IconCanvas
    {
        private readonly int size;
        private readonly Color[] pixels;

        public IconCanvas(int size, bool proSkin)
        {
            this.size = size;
            pixels = new Color[size * size];

            Ink = proSkin ? new Color(0.93f, 0.96f, 0.98f, 1f) : new Color(0.10f, 0.14f, 0.16f, 1f);
            MutedInk = proSkin ? new Color(0.67f, 0.74f, 0.78f, 1f) : new Color(0.34f, 0.39f, 0.43f, 1f);
            Surface = proSkin ? new Color(0.16f, 0.18f, 0.20f, 1f) : new Color(0.96f, 0.97f, 0.98f, 1f);
            Highlight = new Color(0.97f, 0.82f, 0.32f, 1f);
            Shadow = new Color(0f, 0f, 0f, proSkin ? 0.35f : 0.18f);

            Clear(new Color(0f, 0f, 0f, 0f));
        }

        public Color Ink { get; }

        public Color MutedInk { get; }

        public Color Surface { get; }

        public Color Highlight { get; }

        public Color Shadow { get; }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public Texture2D ToTexture(int outputSize)
        {
            if (outputSize >= size)
            {
                return ToTexture();
            }

            var texture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var output = new Color[outputSize * outputSize];
            var scale = size / (float)outputSize;

            for (var y = 0; y < outputSize; y++)
            {
                for (var x = 0; x < outputSize; x++)
                {
                    output[y * outputSize + x] = SampleArea(x * scale, y * scale, (x + 1) * scale, (y + 1) * scale);
                }
            }

            texture.SetPixels(output);
            texture.Apply(false, true);
            return texture;
        }

        public void Clear(Color color)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
        }

        public void FillRect(float x, float y, float width, float height, Color color)
        {
            var minX = ToPixelFloor(x);
            var minY = ToPixelFloor(y);
            var maxX = ToPixelCeil(x + width);
            var maxY = ToPixelCeil(y + height);

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    Blend(px, py, color);
                }
            }
        }

        public void FillRoundedRect(float x, float y, float width, float height, float radius, Color color)
        {
            var minX = ToPixelFloor(x);
            var minY = ToPixelFloor(y);
            var maxX = ToPixelCeil(x + width);
            var maxY = ToPixelCeil(y + height);
            var r = radius * size;

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var nx = (px + 0.5f) / size;
                    var ny = (py + 0.5f) / size;
                    var cx = Mathf.Clamp(nx, x + radius, x + width - radius);
                    var cy = Mathf.Clamp(ny, y + radius, y + height - radius);
                    var distance = Vector2.Distance(new Vector2(nx, ny), new Vector2(cx, cy)) * size;

                    if (distance <= r)
                    {
                        Blend(px, py, color);
                    }
                }
            }
        }

        public void FillCircle(float cx, float cy, float radius, Color color)
        {
            var minX = ToPixelFloor(cx - radius);
            var minY = ToPixelFloor(cy - radius);
            var maxX = ToPixelCeil(cx + radius);
            var maxY = ToPixelCeil(cy + radius);
            var r = radius * size;

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var dx = (px + 0.5f) - cx * size;
                    var dy = (py + 0.5f) - cy * size;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var coverage = Mathf.Clamp01(r + 0.65f - distance);

                    if (coverage > 0f)
                    {
                        Blend(px, py, WithAlpha(color, color.a * coverage));
                    }
                }
            }
        }

        public void DrawCircle(float cx, float cy, float radius, Color color)
        {
            DrawArc(cx, cy, radius, 0, 360, color, 0.025f);
        }

        public void DrawArc(float cx, float cy, float radius, float startDegrees, float endDegrees, Color color, float thickness)
        {
            var span = Mathf.Abs(endDegrees - startDegrees);
            var steps = Mathf.Max(8, Mathf.CeilToInt(span / 12f));
            var previousX = cx + Mathf.Cos(startDegrees * Mathf.Deg2Rad) * radius;
            var previousY = cy + Mathf.Sin(startDegrees * Mathf.Deg2Rad) * radius;

            for (var i = 1; i <= steps; i++)
            {
                var t = i / (float)steps;
                var degrees = Mathf.Lerp(startDegrees, endDegrees, t);
                var x = cx + Mathf.Cos(degrees * Mathf.Deg2Rad) * radius;
                var y = cy + Mathf.Sin(degrees * Mathf.Deg2Rad) * radius;
                DrawLine(previousX, previousY, x, y, color, thickness);
                previousX = x;
                previousY = y;
            }
        }

        public void DrawLine(float x0, float y0, float x1, float y1, Color color, float thickness)
        {
            var minX = ToPixelFloor(Mathf.Min(x0, x1) - thickness);
            var minY = ToPixelFloor(Mathf.Min(y0, y1) - thickness);
            var maxX = ToPixelCeil(Mathf.Max(x0, x1) + thickness);
            var maxY = ToPixelCeil(Mathf.Max(y0, y1) + thickness);
            var ax = x0 * size;
            var ay = y0 * size;
            var bx = x1 * size;
            var by = y1 * size;
            var radius = Mathf.Max(0.5f, thickness * size * 0.5f);

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var distance = DistanceToSegment(px + 0.5f, py + 0.5f, ax, ay, bx, by);
                    var coverage = Mathf.Clamp01(radius + 0.65f - distance);

                    if (coverage > 0f)
                    {
                        Blend(px, py, WithAlpha(color, color.a * coverage));
                    }
                }
            }
        }

        public void FillTriangle(float x0, float y0, float x1, float y1, float x2, float y2, Color color)
        {
            var minX = ToPixelFloor(Mathf.Min(x0, Mathf.Min(x1, x2)));
            var minY = ToPixelFloor(Mathf.Min(y0, Mathf.Min(y1, y2)));
            var maxX = ToPixelCeil(Mathf.Max(x0, Mathf.Max(x1, x2)));
            var maxY = ToPixelCeil(Mathf.Max(y0, Mathf.Max(y1, y2)));

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var x = (px + 0.5f) / size;
                    var y = (py + 0.5f) / size;

                    if (PointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                    {
                        Blend(px, py, color);
                    }
                }
            }
        }

        private void Blend(int x, int y, Color source)
        {
            if (x < 0 || y < 0 || x >= size || y >= size || source.a <= 0f)
            {
                return;
            }

            var index = y * size + x;
            var destination = pixels[index];
            var alpha = source.a + destination.a * (1f - source.a);

            if (alpha <= 0f)
            {
                pixels[index] = new Color(0f, 0f, 0f, 0f);
                return;
            }

            pixels[index] = new Color(
                (source.r * source.a + destination.r * destination.a * (1f - source.a)) / alpha,
                (source.g * source.a + destination.g * destination.a * (1f - source.a)) / alpha,
                (source.b * source.a + destination.b * destination.a * (1f - source.a)) / alpha,
                alpha);
        }

        private Color SampleArea(float minX, float minY, float maxX, float maxY)
        {
            var x0 = Mathf.FloorToInt(minX);
            var y0 = Mathf.FloorToInt(minY);
            var x1 = Mathf.CeilToInt(maxX);
            var y1 = Mathf.CeilToInt(maxY);
            var totalWeight = 0f;
            var r = 0f;
            var g = 0f;
            var b = 0f;
            var a = 0f;

            for (var y = y0; y < y1; y++)
            {
                if (y < 0 || y >= size)
                {
                    continue;
                }

                var sampleMinY = Mathf.Max(minY, y);
                var sampleMaxY = Mathf.Min(maxY, y + 1f);
                var height = sampleMaxY - sampleMinY;

                if (height <= 0f)
                {
                    continue;
                }

                for (var x = x0; x < x1; x++)
                {
                    if (x < 0 || x >= size)
                    {
                        continue;
                    }

                    var sampleMinX = Mathf.Max(minX, x);
                    var sampleMaxX = Mathf.Min(maxX, x + 1f);
                    var width = sampleMaxX - sampleMinX;

                    if (width <= 0f)
                    {
                        continue;
                    }

                    var weight = width * height;
                    var color = pixels[y * size + x];
                    r += color.r * weight;
                    g += color.g * weight;
                    b += color.b * weight;
                    a += color.a * weight;
                    totalWeight += weight;
                }
            }

            if (totalWeight <= Mathf.Epsilon)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            return new Color(r / totalWeight, g / totalWeight, b / totalWeight, a / totalWeight);
        }

        private int ToPixelFloor(float value)
        {
            return Mathf.FloorToInt(value * size);
        }

        private int ToPixelCeil(float value)
        {
            return Mathf.CeilToInt(value * size);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var lengthSquared = dx * dx + dy * dy;

            if (lengthSquared <= Mathf.Epsilon)
            {
                var ox = px - ax;
                var oy = py - ay;
                return Mathf.Sqrt(ox * ox + oy * oy);
            }

            var t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lengthSquared);
            var projectionX = ax + t * dx;
            var projectionY = ay + t * dy;
            var x = px - projectionX;
            var y = py - projectionY;
            return Mathf.Sqrt(x * x + y * y);
        }

        private static bool PointInTriangle(float px, float py, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            var d0 = Sign(px, py, x0, y0, x1, y1);
            var d1 = Sign(px, py, x1, y1, x2, y2);
            var d2 = Sign(px, py, x2, y2, x0, y0);
            var hasNegative = d0 < 0f || d1 < 0f || d2 < 0f;
            var hasPositive = d0 > 0f || d1 > 0f || d2 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(float px, float py, float ax, float ay, float bx, float by)
        {
            return (px - bx) * (ay - by) - (ax - bx) * (py - by);
        }
    }
}
