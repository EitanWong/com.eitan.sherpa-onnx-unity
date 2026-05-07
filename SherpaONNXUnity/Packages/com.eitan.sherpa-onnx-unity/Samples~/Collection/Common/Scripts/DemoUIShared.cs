namespace Eitan.SherpaONNXUnity.Samples
{
    using Eitan.SherpaONNXUnity.Runtime;
    using UnityEngine;
    using UnityEngine.UI;
    using Stage = Eitan.SherpaONNXUnity.Samples.ModelLoadProgressTracker.Stage;

    /// <summary>
    /// Shared UI helpers and color palette for demo scenes.
    /// 演示场景通用的UI辅助方法与颜色配置。
    /// </summary>
    public static class DemoUIShared
    {
        // 统一颜色配置 / Centralized palette
        public static readonly Color LoadColor = new Color(0.15f, 0.67f, 0.36f);
        public static readonly Color UnloadColor = new Color(0.83f, 0.27f, 0.27f);
        public static readonly Color RecordIdleColor = new Color(0.2f, 0.6f, 0.95f);
        public static readonly Color RecordStopColor = new Color(0.93f, 0.45f, 0.2f);
        public static readonly Color DisabledColor = new Color(0.65f, 0.65f, 0.65f);

        private readonly struct LanguageOption
        {
            public LanguageOption(string code, string label)
            {
                Code = code;
                Label = label;
            }

            public string Code { get; }
            public string Label { get; }
        }

        private static readonly LanguageOption[] CohereLanguages =
        {
            new LanguageOption("en", "English (en)"),
            new LanguageOption("fr", "French (fr)"),
            new LanguageOption("de", "German (de)"),
            new LanguageOption("it", "Italian (it)"),
            new LanguageOption("es", "Spanish (es)"),
            new LanguageOption("pt", "Portuguese (pt)"),
            new LanguageOption("el", "Greek (el)"),
            new LanguageOption("nl", "Dutch (nl)"),
            new LanguageOption("pl", "Polish (pl)"),
            new LanguageOption("zh", "Chinese, Mandarin (zh)"),
            new LanguageOption("ja", "Japanese (ja)"),
            new LanguageOption("ko", "Korean (ko)"),
            new LanguageOption("vi", "Vietnamese (vi)"),
            new LanguageOption("ar", "Arabic (ar)")
        };

        private static readonly LanguageOption[] SenseVoiceLanguages =
        {
            new LanguageOption("auto", "Auto (auto)"),
            new LanguageOption("zh", "Chinese, Mandarin (zh)"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("yue", "Chinese, Cantonese (yue)"),
            new LanguageOption("ja", "Japanese (ja)"),
            new LanguageOption("ko", "Korean (ko)"),
            new LanguageOption("nospeech", "No Speech (nospeech)")
        };

        private static readonly LanguageOption[] FunAsrNanoLanguages =
        {
            new LanguageOption(string.Empty, "Model Default"),
            new LanguageOption("zh", "Chinese (zh)"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("ja", "Japanese (ja)")
        };

        private static readonly LanguageOption[] CanaryLanguages =
        {
            new LanguageOption(string.Empty, "Model Default"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("de", "German (de)"),
            new LanguageOption("fr", "French (fr)"),
            new LanguageOption("es", "Spanish (es)")
        };

        private static readonly LanguageOption[] WhisperLanguages =
        {
            new LanguageOption(string.Empty, "Model Default"),
            new LanguageOption("en", "English (en)"),
            new LanguageOption("zh", "Chinese (zh)"),
            new LanguageOption("de", "German (de)"),
            new LanguageOption("es", "Spanish (es)"),
            new LanguageOption("ru", "Russian (ru)"),
            new LanguageOption("ko", "Korean (ko)"),
            new LanguageOption("fr", "French (fr)"),
            new LanguageOption("ja", "Japanese (ja)"),
            new LanguageOption("pt", "Portuguese (pt)"),
            new LanguageOption("tr", "Turkish (tr)"),
            new LanguageOption("pl", "Polish (pl)"),
            new LanguageOption("ca", "Catalan (ca)"),
            new LanguageOption("nl", "Dutch (nl)"),
            new LanguageOption("ar", "Arabic (ar)"),
            new LanguageOption("sv", "Swedish (sv)"),
            new LanguageOption("it", "Italian (it)"),
            new LanguageOption("id", "Indonesian (id)"),
            new LanguageOption("hi", "Hindi (hi)"),
            new LanguageOption("fi", "Finnish (fi)"),
            new LanguageOption("vi", "Vietnamese (vi)"),
            new LanguageOption("iw", "Hebrew (iw)"),
            new LanguageOption("uk", "Ukrainian (uk)"),
            new LanguageOption("el", "Greek (el)"),
            new LanguageOption("ms", "Malay (ms)"),
            new LanguageOption("cs", "Czech (cs)"),
            new LanguageOption("ro", "Romanian (ro)"),
            new LanguageOption("da", "Danish (da)"),
            new LanguageOption("hu", "Hungarian (hu)"),
            new LanguageOption("ta", "Tamil (ta)"),
            new LanguageOption("no", "Norwegian (no)"),
            new LanguageOption("th", "Thai (th)"),
            new LanguageOption("ur", "Urdu (ur)"),
            new LanguageOption("hr", "Croatian (hr)"),
            new LanguageOption("bg", "Bulgarian (bg)"),
            new LanguageOption("lt", "Lithuanian (lt)"),
            new LanguageOption("la", "Latin (la)"),
            new LanguageOption("mi", "Maori (mi)"),
            new LanguageOption("ml", "Malayalam (ml)"),
            new LanguageOption("cy", "Welsh (cy)"),
            new LanguageOption("sk", "Slovak (sk)"),
            new LanguageOption("te", "Telugu (te)"),
            new LanguageOption("fa", "Persian (fa)"),
            new LanguageOption("lv", "Latvian (lv)"),
            new LanguageOption("bn", "Bengali (bn)"),
            new LanguageOption("sr", "Serbian (sr)"),
            new LanguageOption("az", "Azerbaijani (az)"),
            new LanguageOption("sl", "Slovenian (sl)"),
            new LanguageOption("kn", "Kannada (kn)"),
            new LanguageOption("et", "Estonian (et)"),
            new LanguageOption("mk", "Macedonian (mk)"),
            new LanguageOption("br", "Breton (br)"),
            new LanguageOption("eu", "Basque (eu)"),
            new LanguageOption("is", "Icelandic (is)"),
            new LanguageOption("hy", "Armenian (hy)"),
            new LanguageOption("ne", "Nepali (ne)"),
            new LanguageOption("mn", "Mongolian (mn)"),
            new LanguageOption("bs", "Bosnian (bs)"),
            new LanguageOption("kk", "Kazakh (kk)"),
            new LanguageOption("sq", "Albanian (sq)"),
            new LanguageOption("sw", "Swahili (sw)"),
            new LanguageOption("gl", "Galician (gl)"),
            new LanguageOption("mr", "Marathi (mr)"),
            new LanguageOption("pa", "Punjabi (pa)"),
            new LanguageOption("si", "Sinhala (si)"),
            new LanguageOption("km", "Khmer (km)"),
            new LanguageOption("sn", "Shona (sn)"),
            new LanguageOption("yo", "Yoruba (yo)"),
            new LanguageOption("so", "Somali (so)"),
            new LanguageOption("af", "Afrikaans (af)"),
            new LanguageOption("oc", "Occitan (oc)"),
            new LanguageOption("ka", "Georgian (ka)"),
            new LanguageOption("be", "Belarusian (be)"),
            new LanguageOption("tg", "Tajik (tg)"),
            new LanguageOption("sd", "Sindhi (sd)"),
            new LanguageOption("gu", "Gujarati (gu)"),
            new LanguageOption("am", "Amharic (am)"),
            new LanguageOption("yi", "Yiddish (yi)"),
            new LanguageOption("lo", "Lao (lo)"),
            new LanguageOption("uz", "Uzbek (uz)"),
            new LanguageOption("fo", "Faroese (fo)"),
            new LanguageOption("ht", "Haitian Creole (ht)"),
            new LanguageOption("ps", "Pashto (ps)"),
            new LanguageOption("tk", "Turkmen (tk)"),
            new LanguageOption("nn", "Nynorsk (nn)"),
            new LanguageOption("mt", "Maltese (mt)"),
            new LanguageOption("sa", "Sanskrit (sa)"),
            new LanguageOption("lb", "Luxembourgish (lb)"),
            new LanguageOption("my", "Myanmar (my)"),
            new LanguageOption("bo", "Tibetan (bo)"),
            new LanguageOption("tl", "Tagalog (tl)"),
            new LanguageOption("mg", "Malagasy (mg)"),
            new LanguageOption("as", "Assamese (as)"),
            new LanguageOption("tt", "Tatar (tt)"),
            new LanguageOption("haw", "Hawaiian (haw)"),
            new LanguageOption("ln", "Lingala (ln)"),
            new LanguageOption("ha", "Hausa (ha)"),
            new LanguageOption("ba", "Bashkir (ba)"),
            new LanguageOption("jw", "Javanese (jw)"),
            new LanguageOption("su", "Sundanese (su)")
        };

        /// <summary>
        /// Set button tint safely. 安全设置按钮颜色。
        /// </summary>
        public static void SetButtonColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        public static Dropdown EnsureLanguageDropdown(Dropdown languageDropdown, Dropdown modelDropdown)
        {
            if (languageDropdown != null || modelDropdown == null)
            {
                return languageDropdown;
            }

            var instance = UnityEngine.Object.Instantiate(modelDropdown.gameObject, modelDropdown.transform.parent);
            instance.name = "Dropdown (Language)";
            var transform = instance.GetComponent<RectTransform>();
            if (transform != null)
            {
                transform.anchoredPosition += new Vector2(0f, -64f);
            }

            var dropdown = instance.GetComponent<Dropdown>();
            dropdown.onValueChanged.RemoveAllListeners();
            ApplyDropdownListDirectionDown(dropdown);
            return dropdown;
        }

        public static void ConfigureSpeechLanguageDropdown(Dropdown languageDropdown, string modelId)
        {
            if (languageDropdown == null)
            {
                return;
            }

            ApplyDropdownListDirectionDown(languageDropdown);

            var options = GetSpeechLanguageOptions(modelId);
            if (options == null)
            {
                languageDropdown.gameObject.SetActive(false);
                return;
            }

            languageDropdown.gameObject.SetActive(true);
            languageDropdown.options.Clear();
            for (var i = 0; i < options.Length; i++)
            {
                languageDropdown.options.Add(new Dropdown.OptionData(options[i].Label));
            }

            languageDropdown.value = 0;
            languageDropdown.RefreshShownValue();
        }

        public static string GetSelectedSpeechLanguage(Dropdown languageDropdown, string modelId)
        {
            var options = GetSpeechLanguageOptions(modelId);
            if (languageDropdown == null || options == null || options.Length == 0)
            {
                return string.Empty;
            }

            var index = Mathf.Clamp(languageDropdown.value, 0, options.Length - 1);
            return options[index].Code;
        }

        private static LanguageOption[] GetSpeechLanguageOptions(string modelId)
        {
            var lower = (modelId ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower))
            {
                return null;
            }

            if (lower.Contains("cohere"))
            {
                return CohereLanguages;
            }

            if (lower.Contains("sense-voice") || lower.Contains("sensevoice"))
            {
                return SenseVoiceLanguages;
            }

            if (lower.Contains("funasr-nano") || lower.Contains("funasr_nano") || lower.Contains("funasr"))
            {
                return FunAsrNanoLanguages;
            }

            if (lower.Contains("whisper"))
            {
                return WhisperLanguages;
            }

            if (lower.Contains("nemo-canary") || lower.Contains("canary"))
            {
                return CanaryLanguages;
            }

            return null;
        }

        private static void ApplyDropdownListDirectionDown(Dropdown dropdown)
        {
            if (dropdown == null || dropdown.template == null)
            {
                return;
            }

            var template = dropdown.template;
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(template.sizeDelta.x, 300f);
        }

        /// <summary>
        /// Show loading progress and update status text. 展示加载进度并更新状态文本。
        /// </summary>
        public static void ShowLoading(ModelLoadProgressTracker tracker, Text statusText, string message)
        {
            tracker?.Reset();
            tracker?.SetVisible(true);
            tracker?.MarkStageComplete(Stage.Prepare, message);
            tracker?.UpdateStage(Stage.Download, message, 0.35f);
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// Hide loading UI and finalize status. 隐藏加载进度并更新完成状态。
        /// </summary>
        public static void ShowLoadingComplete(ModelLoadProgressTracker tracker, Text statusText, string message)
        {
            tracker?.Complete(message);
            tracker?.SetVisible(false);
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// Update the shared progress tracker based on structured feedback from the runtime.
        /// </summary>
        public static void UpdateProgressFromFeedback(ModelLoadProgressTracker tracker, Text statusText, SherpaFeedback feedback)
        {
            if (feedback == null)
            {
                return;
            }

            tracker?.SetVisible(true);

            var message = feedback.Message ?? string.Empty;
            statusText?.gameObject.SetActive(true);
            if (statusText != null)
            {
                statusText.text = message;
            }

            switch (feedback)
            {
                case PrepareFeedback prepare:
                    tracker?.UpdateStage(Stage.Prepare, message, 0.05f);
                    break;
                case DownloadFeedback download:
                    tracker?.UpdateStage(Stage.Download, message, download.Progress);
                    break;
                case VerifyFeedback verify:
                    tracker?.UpdateStage(Stage.Verify, message, verify.Progress);
                    break;
                case DecompressFeedback decompress:
                    tracker?.UpdateStage(Stage.Decompress, message, decompress.Progress);
                    break;
                case CleanFeedback clean:
                    tracker?.UpdateStage(Stage.Clean, message, 1f);
                    break;
                case LoadFeedback load:
                    tracker?.UpdateStage(Stage.Load, message, 0.75f);
                    break;
                case SuccessFeedback success:
                    tracker?.Complete(message);
                    tracker?.SetVisible(false);
                    break;
                case FailedFeedback failed:
                    tracker?.UpdateStage(Stage.Load, message, 1f);
                    tracker?.SetVisible(true);
                    break;
                case CancelFeedback cancel:
                    tracker?.UpdateStage(Stage.Load, message, 0f);
                    tracker?.SetVisible(true);
                    break;
            }
        }
    }
}
