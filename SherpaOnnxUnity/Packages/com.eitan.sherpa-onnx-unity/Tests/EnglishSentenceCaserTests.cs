// Packages/com.eitan.sherpa-onnx-unity/Tests/EnglishSentenceCaserTests.cs
using NUnit.Framework;
using Eitan.SherpaOnnxUnity.Runtime.Utilities.Lexicon;

namespace Eitan.SherpaOnnxUnity.Tests
{
    [TestFixture]
    public class EnglishSentenceCaserTests
    {
        private static void Expect(string input, string expected)
        {
            var actual = EnglishSentenceCaser.ToSentenceCase(input);
            Assert.AreEqual(expected, actual, $"Input:\n{input}\n");
        }

        [Test]
        public void DirtyMixedCases_BasicAndGreedyPhrases()
        {
            Expect(
                "HELLO WORLD. THIS IS A TEST.",
                "Hello world. This is a test.");

            // 贪心短语匹配（New York City）+ 缩略词（NASA、FBI）
            Expect(
                "TODAY I WENT TO NEW YORK CITY, AND I SAW NASA AND THE FBI.",
                "Today I went to New York City, and I saw NASA and the FBI.");

            // 多词短语（Visual Studio Code、OpenAI API）+ 全大写词（ONNX、GPU）
            Expect(
                "WE USE VISUAL STUDIO CODE WITH OPENAI API, ONNX AND GPU ACCELERATION.",
                "We use Visual Studio Code with OpenAI API, ONNX and GPU acceleration.");
        }

        [Test]
        public void DirtyMixedCases_PunctuationQuotesHyphensContractions()
        {
            // 破折号 + 连字符 + emoji + 缩写
            Expect(
                "DON'T PANIC — IT'S STATE-OF-THE-ART 😂!",
                "Don't panic — it's state-of-the-art 😂!");

            // 引号与逗号、句中小写、专有名
            Expect(
                "\"THIS, TOO, SHALL PASS,\" SAID JAMES.",
                "\"This, too, shall pass,\" said James.");

            // 省略号与问句
            Expect(
                "WAIT... ARE YOU SURE?",
                "Wait... Are you sure?");
        }

        [Test]
        public void DirtyMixedCases_TitlesHolidaysDaysMonths()
        {
            // 称谓（Dr、Prof）+ 星期/月 + 地名
            Expect(
                "I MET DR SMITH AND PROF JOHNSON ON MONDAY IN PARIS.",
                "I met Dr Smith and Prof Johnson on Monday in Paris.");

            // 节日大写，季节小写
            Expect(
                "HAPPY HALLOWEEN. IN SPRING WE PLANT TREES.",
                "Happy Halloween. In spring we plant trees.");
        }

        [Test]
        public void DirtyMixedCases_NumbersTimeWhitespace()
        {
            // 数字/百分号/日期/时间；普通词回退小写
            Expect(
                "RESULTS: 99.9% ACCURACY ON 2025-10-14 AT 08:00.",
                "Results: 99.9% accuracy on 2025-10-14 at 08:00.");

            // 多空白折叠（实现会折叠成单空格）
            Expect(
                "HELLO   NEW   YORK",
                "Hello New York");

            // 连续感叹与问号，句界切换
            Expect(
                "WOW!!! ARE YOU READY?? YES!!!",
                "Wow!!! Are you ready?? Yes!!!");
        }

        [Test]
        public void Idempotence_SecondPassDoesNotChange()
        {
            var once = EnglishSentenceCaser.ToSentenceCase(
                "I MET DR SMITH IN NEW YORK. WE USED OPENAI API.");
            var twice = EnglishSentenceCaser.ToSentenceCase(once);
            Assert.AreEqual(once, twice, "ToSentenceCase should be idempotent.");
        }

        [Test]
        public void MixedCase_WithPhrasesAndAcronyms()
        {
            // 输入大小写混杂，包含短语与缩略词
            Expect(
                "HeLLo WORLD. we USE OPENAI api and visual STUDIO code in NEW york city.",
                "Hello world. We use OpenAI API and Visual Studio Code in New York City.");
        }

        [Test]
        public void NoPunctuation_BasicSentence()
        {
            // 无任何标点，只有空格分词
            Expect(
                "HELLO WORLD THIS IS A TEST",
                "Hello world this is a test");
        }

        [Test]
        public void NoPunctuation_PhrasesAndAcronyms()
        {
            // 无标点但包含短语与缩略词
            Expect(
                "TODAY I WENT TO NEW YORK CITY AND SAW NASA AND THE FBI",
                "Today I went to New York City and saw NASA and the FBI");
        }

        [Test]
        public void NoPunctuation_IPronounAndLibraries()
        {
            // 无标点，句中 I 与缩略词/库名
            Expect(
                "i love gpu and onnx with visual studio code",
                "I love GPU and ONNX with Visual Studio Code");
        }

        [Test]
        public void NoPunctuation_WhitespaceFold_MixedCase()
        {
            // 多个空格 + 混合大小写；应折叠空白并规范大小写
            Expect(
                "  tHiS   is   NEW   YORK   ",
                "This is New York");
        }
        [Test]
        public void MixedLanguage_ZhEn_WithSpaces_PhrasesAcronyms()
        {
            // 中文 + 英文（带空格）+ 短语 + 缩略词
            Expect(
                "今天 我们 去了 NEW YORK CITY 并 使用 OPENAI API",
                "今天 我们 去了 New York City 并 使用 OpenAI API");
        }

        [Test]
        public void MixedLanguage_JaEn_WithSpaces_PhrasesAcronyms()
        {
            // 日文 + 英文（带空格）+ 缩略词 + 专有名词
            Expect(
                "今日は TOKYO で GPU と ONNX を 使う",
                "今日は Tokyo で GPU と ONNX を 使う");
        }

        [Test]
        public void MixedLanguage_KoEn_WithSpaces_Phrases()
        {
            // 韩文 + 英文短语（带空格）
            Expect(
                "오늘 우리는 VISUAL STUDIO CODE 를 사용",
                "오늘 우리는 Visual Studio Code 를 사용");
        }

        [Test]
        public void MixedLanguage_ArEn_WithSpaces_Acronyms()
        {
            // 阿拉伯文 + 英文（带空格）+ 缩略词
            Expect(
                "اليوم نستخدم OPENAI API في العمل",
                "اليوم نستخدم OpenAI API في العمل");
        }

        [Test]
        public void MixedLanguage_HiEn_WithSpaces_Acronyms()
        {
            // 印地语 + 英文（带空格）+ 缩略词
            Expect(
                "आज हम GPU और ONNX का उपयोग करते हैं",
                "आज हम GPU और ONNX का उपयोग करते हैं");
        }

        [Test]
        public void NonEnglish_PureChinese_NoPunctuation_Unchanged()
        {
            // 纯中文，无标点；应保持不变（对中文无大小写影响）
            Expect(
                "今天我们去北京",
                "今天我们去北京");
        }

        [Test]
        public void MixedLanguage_RuEn_SentenceStartCapitalization()
        {
            // 俄文句首会被大写（有大小写概念），英文短语/缩略词按规则处理
            Expect(
                "сегодня мы используем OPENAI API для теста",
                "Сегодня мы используем OpenAI API для теста");
        }

        [Test]
        public void MixedLanguage_CJK_WithEmojiAndSeparators()
        {
            // CJK + Emoji + 英文缩略词；应保持 CJK 原样，英文缩略词规则生效
            Expect(
                "我们 使用 GPU 😀 和 ONNX 进行 推理",
                "我们 使用 GPU 😀 和 ONNX 进行 推理");
        }
    }
}