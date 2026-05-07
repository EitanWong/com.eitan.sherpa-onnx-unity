namespace Eitan.Sherpa.Onnx.Unity.Editor.Mono.Icons
{
    using UnityEngine;

    internal static class SherpaMonoIconPainter
    {
        private static readonly Color White = new Color(0.98f, 0.99f, 1f, 1f);
        private static readonly Color SoftWhite = new Color(0.82f, 0.92f, 1f, 1f);
        private static readonly Color DarkStroke = new Color(0.04f, 0.07f, 0.09f, 0.28f);

        public static Texture2D Paint(SherpaMonoIconId iconId, int size, bool proSkin)
        {
            var renderScale = size <= 32 ? 4 : 2;
            var canvas = new IconCanvas(size * renderScale, proSkin);
            var accent = AccentFor(iconId);
            DrawFrame(canvas, accent);
            DrawGlyph(canvas, iconId);
            DrawCornerMark(canvas, iconId);
            return canvas.ToTexture(size);
        }

        private static void DrawFrame(IconCanvas canvas, Color accent)
        {
            canvas.FillRoundedRect(0.08f, 0.08f, 0.84f, 0.84f, 0.18f, new Color(0f, 0f, 0f, 0.20f));
            canvas.FillRoundedRect(0.10f, 0.08f, 0.80f, 0.80f, 0.17f, accent);
            canvas.FillRoundedRect(0.14f, 0.14f, 0.72f, 0.66f, 0.14f, Shift(accent, 0.10f));
            canvas.FillRoundedRect(0.18f, 0.20f, 0.64f, 0.50f, 0.10f, new Color(0f, 0f, 0f, 0.08f));
        }

        private static void DrawGlyph(IconCanvas canvas, SherpaMonoIconId iconId)
        {
            switch (iconId)
            {
                case SherpaMonoIconId.MicrophoneInput:
                    DrawMicrophone(canvas);
                    break;
                case SherpaMonoIconId.RealtimeSpeechRecognizer:
                    DrawRecognizer(canvas, true);
                    break;
                case SherpaMonoIconId.OfflineSpeechRecognizer:
                    DrawRecognizer(canvas, false);
                    break;
                case SherpaMonoIconId.SpeechSynthesizer:
                    DrawSpeechSynthesis(canvas, false);
                    break;
                case SherpaMonoIconId.ZeroShotSpeechSynthesizer:
                    DrawSpeechSynthesis(canvas, true);
                    break;
                case SherpaMonoIconId.SpeechEnhancement:
                    DrawEnhancement(canvas);
                    break;
                case SherpaMonoIconId.SourceSeparation:
                    DrawSourceSeparation(canvas);
                    break;
                case SherpaMonoIconId.KeywordSpotting:
                    DrawKeyword(canvas);
                    break;
                case SherpaMonoIconId.SpokenLanguageIdentification:
                    DrawLanguage(canvas);
                    break;
                case SherpaMonoIconId.SpeakerDiarization:
                    DrawDiarization(canvas);
                    break;
                case SherpaMonoIconId.Punctuation:
                    DrawPunctuation(canvas);
                    break;
                case SherpaMonoIconId.AudioTagging:
                    DrawTag(canvas);
                    break;
                case SherpaMonoIconId.VoiceActivityDetection:
                    DrawVad(canvas);
                    break;
                case SherpaMonoIconId.RuntimeSettings:
                    DrawRuntimeSettings(canvas);
                    break;
                case SherpaMonoIconId.CustomModels:
                    DrawCustomModels(canvas);
                    break;
                default:
                    DrawSherpa(canvas);
                    break;
            }
        }

        private static void DrawCornerMark(IconCanvas canvas, SherpaMonoIconId iconId)
        {
            switch (iconId)
            {
                case SherpaMonoIconId.RealtimeSpeechRecognizer:
                    DrawLiveDot(canvas);
                    break;
                case SherpaMonoIconId.OfflineSpeechRecognizer:
                    DrawStorageMark(canvas);
                    break;
                case SherpaMonoIconId.ZeroShotSpeechSynthesizer:
                    DrawSpark(canvas, 0.73f, 0.73f, 0.06f, White);
                    break;
                case SherpaMonoIconId.Sherpa:
                    canvas.FillCircle(0.73f, 0.73f, 0.045f, SoftWhite);
                    break;
            }
        }

        private static void DrawSherpa(IconCanvas canvas)
        {
            canvas.FillCircle(0.34f, 0.43f, 0.075f, White);
            canvas.FillCircle(0.50f, 0.30f, 0.075f, White);
            canvas.FillCircle(0.66f, 0.43f, 0.075f, White);
            canvas.FillCircle(0.50f, 0.66f, 0.075f, White);
            canvas.DrawLine(0.34f, 0.43f, 0.50f, 0.30f, White, 0.065f);
            canvas.DrawLine(0.50f, 0.30f, 0.66f, 0.43f, White, 0.065f);
            canvas.DrawLine(0.34f, 0.43f, 0.50f, 0.66f, White, 0.065f);
            canvas.DrawLine(0.66f, 0.43f, 0.50f, 0.66f, White, 0.065f);
            canvas.FillCircle(0.50f, 0.49f, 0.09f, SoftWhite);
        }

        private static void DrawMicrophone(IconCanvas canvas)
        {
            ShadowLine(canvas, 0.50f, 0.32f, 0.50f, 0.24f, 0.075f);
            canvas.FillRoundedRect(0.40f, 0.40f, 0.20f, 0.32f, 0.095f, White);
            canvas.FillRoundedRect(0.455f, 0.49f, 0.09f, 0.14f, 0.04f, SoftWhite);
            canvas.DrawArc(0.50f, 0.44f, 0.23f, 205, 335, White, 0.07f);
            canvas.DrawLine(0.50f, 0.32f, 0.50f, 0.24f, White, 0.07f);
            canvas.DrawLine(0.36f, 0.24f, 0.64f, 0.24f, White, 0.07f);
        }

        private static void DrawRecognizer(IconCanvas canvas, bool realtime)
        {
            canvas.FillRoundedRect(0.26f, 0.43f, 0.15f, 0.27f, 0.07f, White);
            canvas.DrawArc(0.335f, 0.43f, 0.15f, 205, 335, White, 0.048f);
            canvas.DrawLine(0.335f, 0.31f, 0.335f, 0.25f, White, 0.048f);
            canvas.DrawLine(0.265f, 0.25f, 0.405f, 0.25f, White, 0.048f);
            DrawTranscriptLines(canvas, 0.49f, 0.40f, 0.73f, realtime);

            if (realtime)
            {
                DrawMiniWave(canvas, 0.49f, 0.66f, 0.71f, SoftWhite);
                return;
            }

            DrawDocument(canvas, 0.585f, 0.29f, 0.16f, 0.20f);
        }

        private static void DrawSpeaker(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.24f, 0.43f, 0.16f, 0.16f, 0.04f, White);
            FillQuad(canvas, 0.39f, 0.43f, 0.59f, 0.32f, 0.59f, 0.70f, 0.39f, 0.59f, White);
            canvas.DrawLine(0.48f, 0.40f, 0.48f, 0.62f, SoftWhite, 0.032f);
            canvas.DrawArc(0.61f, 0.51f, 0.13f, -42, 42, SoftWhite, 0.052f);
            canvas.DrawArc(0.61f, 0.51f, 0.23f, -42, 42, White, 0.052f);
        }

        private static void DrawSpeechSynthesis(IconCanvas canvas, bool zeroShot)
        {
            var accent = Shift(AccentFor(SherpaMonoIconId.SpeechSynthesizer), 0.12f);
            canvas.FillRoundedRect(0.24f, 0.39f, 0.34f, 0.28f, 0.06f, White);
            canvas.DrawLine(0.30f, 0.58f, 0.52f, 0.58f, accent, 0.04f);
            canvas.DrawLine(0.30f, 0.50f, 0.49f, 0.50f, accent, 0.04f);
            canvas.DrawLine(0.30f, 0.42f, 0.44f, 0.42f, accent, 0.04f);

            canvas.FillTriangle(0.54f, 0.43f, 0.68f, 0.52f, 0.54f, 0.61f, SoftWhite);
            canvas.DrawArc(0.67f, 0.52f, 0.10f, -35, 35, White, 0.045f);
            canvas.DrawArc(0.67f, 0.52f, 0.17f, -35, 35, White, 0.04f);

            if (!zeroShot)
            {
                return;
            }

            canvas.FillRoundedRect(0.24f, 0.25f, 0.20f, 0.10f, 0.035f, SoftWhite);
            DrawSimpleWave(canvas, 0.28f, 0.30f, 0.40f, 0.025f, accent, 0.025f);
            DrawSpark(canvas, 0.70f, 0.70f, 0.055f, White);
        }

        private static void DrawEnhancement(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.25f, 0.42f, 0.48f, 0.17f, 0.085f, White);
            DrawSimpleWave(canvas, 0.31f, 0.505f, 0.67f, 0.07f, Shift(AccentFor(SherpaMonoIconId.SpeechEnhancement), 0.14f), 0.043f);
            canvas.DrawLine(0.36f, 0.33f, 0.46f, 0.27f, SoftWhite, 0.055f);
            canvas.DrawLine(0.46f, 0.27f, 0.62f, 0.27f, SoftWhite, 0.055f);
            canvas.DrawLine(0.62f, 0.27f, 0.72f, 0.33f, SoftWhite, 0.055f);
            DrawSpark(canvas, 0.66f, 0.64f, 0.06f, White);
        }

        private static void DrawSourceSeparation(IconCanvas canvas)
        {
            DrawSimpleWave(canvas, 0.24f, 0.50f, 0.43f, 0.085f, White, 0.052f);
            canvas.DrawLine(0.45f, 0.50f, 0.67f, 0.34f, White, 0.055f);
            canvas.DrawLine(0.45f, 0.50f, 0.67f, 0.66f, White, 0.055f);
            canvas.FillCircle(0.72f, 0.33f, 0.08f, SoftWhite);
            canvas.FillCircle(0.72f, 0.67f, 0.08f, SoftWhite);
        }

        private static void DrawKeyword(IconCanvas canvas)
        {
            canvas.FillCircle(0.36f, 0.43f, 0.13f, White);
            canvas.FillCircle(0.36f, 0.43f, 0.055f, Shift(AccentFor(SherpaMonoIconId.KeywordSpotting), 0.14f));
            canvas.DrawLine(0.455f, 0.515f, 0.70f, 0.75f, White, 0.078f);
            canvas.DrawLine(0.59f, 0.645f, 0.68f, 0.56f, White, 0.052f);
            canvas.DrawCircle(0.63f, 0.34f, 0.105f, SoftWhite);
            canvas.FillCircle(0.63f, 0.34f, 0.032f, SoftWhite);
        }

        private static void DrawLanguage(IconCanvas canvas)
        {
            canvas.FillCircle(0.43f, 0.48f, 0.225f, White);
            canvas.DrawArc(0.43f, 0.48f, 0.14f, 72, 288, Shift(AccentFor(SherpaMonoIconId.SpokenLanguageIdentification), 0.12f), 0.042f);
            canvas.DrawLine(0.225f, 0.48f, 0.635f, 0.48f, Shift(AccentFor(SherpaMonoIconId.SpokenLanguageIdentification), 0.12f), 0.042f);
            canvas.DrawLine(0.43f, 0.265f, 0.43f, 0.695f, Shift(AccentFor(SherpaMonoIconId.SpokenLanguageIdentification), 0.12f), 0.042f);
            canvas.FillRoundedRect(0.555f, 0.565f, 0.22f, 0.14f, 0.043f, SoftWhite);
            canvas.FillTriangle(0.63f, 0.685f, 0.69f, 0.755f, 0.69f, 0.675f, SoftWhite);
        }

        private static void DrawDiarization(IconCanvas canvas)
        {
            canvas.FillCircle(0.35f, 0.61f, 0.10f, White);
            canvas.FillCircle(0.65f, 0.61f, 0.10f, SoftWhite);
            canvas.FillRoundedRect(0.23f, 0.34f, 0.24f, 0.18f, 0.07f, White);
            canvas.FillRoundedRect(0.53f, 0.34f, 0.24f, 0.18f, 0.07f, SoftWhite);
            canvas.DrawLine(0.47f, 0.43f, 0.53f, 0.43f, White, 0.04f);
            DrawSimpleWave(canvas, 0.30f, 0.26f, 0.70f, 0.045f, White, 0.038f);
        }

        private static void DrawPunctuation(IconCanvas canvas)
        {
            canvas.DrawArc(0.40f, 0.61f, 0.14f, -85, 265, White, 0.07f);
            canvas.DrawLine(0.40f, 0.48f, 0.40f, 0.38f, White, 0.07f);
            canvas.FillCircle(0.40f, 0.27f, 0.05f, White);
            canvas.DrawLine(0.61f, 0.69f, 0.61f, 0.37f, SoftWhite, 0.078f);
            canvas.FillCircle(0.61f, 0.27f, 0.05f, SoftWhite);
        }

        private static void DrawTag(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.26f, 0.32f, 0.35f, 0.32f, 0.052f, White);
            canvas.FillTriangle(0.58f, 0.32f, 0.76f, 0.48f, 0.58f, 0.64f, White);
            canvas.FillCircle(0.36f, 0.48f, 0.042f, Shift(AccentFor(SherpaMonoIconId.AudioTagging), 0.12f));
            canvas.FillRoundedRect(0.43f, 0.43f, 0.042f, 0.11f, 0.016f, Shift(AccentFor(SherpaMonoIconId.AudioTagging), 0.12f));
            canvas.FillRoundedRect(0.50f, 0.39f, 0.042f, 0.18f, 0.016f, Shift(AccentFor(SherpaMonoIconId.AudioTagging), 0.12f));
            canvas.FillRoundedRect(0.57f, 0.45f, 0.042f, 0.075f, 0.016f, Shift(AccentFor(SherpaMonoIconId.AudioTagging), 0.12f));
        }

        private static void DrawVad(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.26f, 0.27f, 0.085f, 0.46f, 0.032f, SoftWhite);
            canvas.FillRoundedRect(0.655f, 0.27f, 0.085f, 0.46f, 0.032f, SoftWhite);
            canvas.DrawLine(0.345f, 0.50f, 0.655f, 0.50f, SoftWhite, 0.04f);
            DrawSimpleWave(canvas, 0.35f, 0.50f, 0.65f, 0.14f, White, 0.07f);
        }

        private static void DrawRuntimeSettings(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.27f, 0.27f, 0.46f, 0.46f, 0.07f, White);
            DrawSlider(canvas, 0.35f, 0.65f, 0.58f, 0.46f);
            DrawSlider(canvas, 0.35f, 0.65f, 0.48f, 0.58f);
            DrawSlider(canvas, 0.35f, 0.65f, 0.38f, 0.40f);
            canvas.FillCircle(0.68f, 0.68f, 0.065f, SoftWhite);
            canvas.FillCircle(0.68f, 0.68f, 0.030f, Shift(AccentFor(SherpaMonoIconId.RuntimeSettings), 0.10f));
        }

        private static void DrawCustomModels(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.27f, 0.35f, 0.21f, 0.16f, 0.042f, White);
            canvas.FillRoundedRect(0.52f, 0.35f, 0.21f, 0.16f, 0.042f, White);
            canvas.FillRoundedRect(0.395f, 0.58f, 0.21f, 0.16f, 0.042f, SoftWhite);
            canvas.DrawLine(0.50f, 0.58f, 0.375f, 0.51f, White, 0.04f);
            canvas.DrawLine(0.50f, 0.58f, 0.625f, 0.51f, White, 0.04f);
            canvas.DrawLine(0.47f, 0.26f, 0.53f, 0.26f, SoftWhite, 0.05f);
            canvas.DrawLine(0.50f, 0.23f, 0.50f, 0.29f, SoftWhite, 0.05f);
        }

        private static void ShadowLine(IconCanvas canvas, float x0, float y0, float x1, float y1, float thickness)
        {
            canvas.DrawLine(x0 + 0.015f, y0 + 0.015f, x1 + 0.015f, y1 + 0.015f, DarkStroke, thickness);
        }

        private static void DrawSimpleWave(IconCanvas canvas, float x0, float y, float x1, float amplitude, Color color, float thickness)
        {
            const int Segments = 8;
            var previousX = x0;
            var previousY = y;

            for (var i = 1; i <= Segments; i++)
            {
                var t = i / (float)Segments;
                var x = Mathf.Lerp(x0, x1, t);
                var nextY = y + Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
                canvas.DrawLine(previousX, previousY, x, nextY, color, thickness);
                previousX = x;
                previousY = nextY;
            }
        }

        private static void DrawTranscriptLines(IconCanvas canvas, float x0, float y, float x1, bool realtime)
        {
            var accent = Shift(AccentFor(realtime ? SherpaMonoIconId.RealtimeSpeechRecognizer : SherpaMonoIconId.OfflineSpeechRecognizer), 0.16f);
            canvas.DrawLine(x0, y + 0.15f, x1, y + 0.15f, White, 0.045f);
            canvas.DrawLine(x0, y + 0.06f, x1 - 0.04f, y + 0.06f, White, 0.045f);
            canvas.DrawLine(x0, y - 0.03f, x1 - 0.11f, y - 0.03f, accent, 0.04f);
        }

        private static void DrawMiniWave(IconCanvas canvas, float x0, float y, float x1, Color color)
        {
            canvas.DrawLine(x0, y, x0 + 0.045f, y + 0.045f, color, 0.04f);
            canvas.DrawLine(x0 + 0.045f, y + 0.045f, x0 + 0.09f, y - 0.045f, color, 0.04f);
            canvas.DrawLine(x0 + 0.09f, y - 0.045f, x0 + 0.135f, y + 0.045f, color, 0.04f);
            canvas.DrawLine(x0 + 0.135f, y + 0.045f, x1, y, color, 0.04f);
        }

        private static void DrawDocument(IconCanvas canvas, float x, float y, float width, float height)
        {
            canvas.FillRoundedRect(x, y, width, height, 0.035f, SoftWhite);
            canvas.FillTriangle(x + width * 0.62f, y + height, x + width, y + height * 0.62f, x + width, y + height, White);
            canvas.DrawLine(x + 0.04f, y + height * 0.36f, x + width - 0.04f, y + height * 0.36f, Shift(AccentFor(SherpaMonoIconId.OfflineSpeechRecognizer), 0.10f), 0.025f);
            canvas.DrawLine(x + 0.04f, y + height * 0.53f, x + width - 0.06f, y + height * 0.53f, Shift(AccentFor(SherpaMonoIconId.OfflineSpeechRecognizer), 0.10f), 0.025f);
        }

        private static void DrawLiveDot(IconCanvas canvas)
        {
            canvas.FillCircle(0.74f, 0.73f, 0.07f, White);
            canvas.FillCircle(0.74f, 0.73f, 0.035f, Shift(AccentFor(SherpaMonoIconId.RealtimeSpeechRecognizer), 0.18f));
        }

        private static void DrawStorageMark(IconCanvas canvas)
        {
            canvas.FillRoundedRect(0.67f, 0.66f, 0.15f, 0.12f, 0.03f, White);
            canvas.DrawLine(0.69f, 0.71f, 0.80f, 0.71f, Shift(AccentFor(SherpaMonoIconId.OfflineSpeechRecognizer), 0.10f), 0.02f);
        }

        private static void DrawSlider(IconCanvas canvas, float x0, float x1, float y, float knobX)
        {
            var accent = Shift(AccentFor(SherpaMonoIconId.RuntimeSettings), 0.10f);
            canvas.DrawLine(x0, y, x1, y, accent, 0.035f);
            canvas.FillCircle(knobX, y, 0.045f, accent);
        }

        private static void DrawSpark(IconCanvas canvas, float cx, float cy, float radius, Color color)
        {
            canvas.DrawLine(cx, cy - radius, cx, cy + radius, color, 0.045f);
            canvas.DrawLine(cx - radius, cy, cx + radius, cy, color, 0.045f);
            canvas.DrawLine(cx - radius * 0.62f, cy - radius * 0.62f, cx + radius * 0.62f, cy + radius * 0.62f, color, 0.035f);
            canvas.DrawLine(cx - radius * 0.62f, cy + radius * 0.62f, cx + radius * 0.62f, cy - radius * 0.62f, color, 0.035f);
        }

        private static void FillQuad(
            IconCanvas canvas,
            float x0,
            float y0,
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            Color color)
        {
            canvas.FillTriangle(x0, y0, x1, y1, x2, y2, color);
            canvas.FillTriangle(x0, y0, x2, y2, x3, y3, color);
        }

        private static Color AccentFor(SherpaMonoIconId iconId)
        {
            switch (iconId)
            {
                case SherpaMonoIconId.MicrophoneInput:
                case SherpaMonoIconId.AudioTagging:
                case SherpaMonoIconId.SourceSeparation:
                    return new Color(0.02f, 0.52f, 0.72f, 1f);
                case SherpaMonoIconId.RealtimeSpeechRecognizer:
                case SherpaMonoIconId.OfflineSpeechRecognizer:
                case SherpaMonoIconId.VoiceActivityDetection:
                    return new Color(0.05f, 0.58f, 0.36f, 1f);
                case SherpaMonoIconId.SpeechSynthesizer:
                case SherpaMonoIconId.ZeroShotSpeechSynthesizer:
                    return new Color(0.82f, 0.39f, 0.08f, 1f);
                case SherpaMonoIconId.KeywordSpotting:
                case SherpaMonoIconId.SpeakerDiarization:
                    return new Color(0.43f, 0.33f, 0.78f, 1f);
                case SherpaMonoIconId.SpeechEnhancement:
                    return new Color(0.04f, 0.63f, 0.58f, 1f);
                case SherpaMonoIconId.SpokenLanguageIdentification:
                case SherpaMonoIconId.Punctuation:
                    return new Color(0.78f, 0.23f, 0.34f, 1f);
                case SherpaMonoIconId.RuntimeSettings:
                    return new Color(0.20f, 0.45f, 0.70f, 1f);
                case SherpaMonoIconId.CustomModels:
                    return new Color(0.36f, 0.40f, 0.74f, 1f);
                default:
                    return new Color(0.12f, 0.39f, 0.68f, 1f);
            }
        }

        private static Color Shift(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }
    }
}
