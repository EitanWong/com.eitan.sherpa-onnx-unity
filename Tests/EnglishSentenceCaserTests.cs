// Packages/com.eitan.sherpa-onnx-unity/Tests/EnglishSentenceCaserTests.cs
using Eitan.SherpaONNXUnity.Runtime.Utilities.Lexicon;
using NUnit.Framework;

namespace Eitan.SherpaONNXUnity.Tests
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
        // ---------------- Apostrophe 's reconstruction tests ----------------
        [Test]
        public void Contractions_AposS_Reconstruction_It_Is_Basics()
        {
            // 基础正例：ITS -> It's（句首 + 提示词）
            Expect("ITS OK", "It's ok");
            Expect("ITS A NICE DAY", "It's a nice day");
            Expect("ITS BEEN GREAT", "It's been great");
            Expect("ITS GOING WELL", "It's going well");
            Expect("ITS VERY GOOD", "It's very good");
            // 句中也可触发（前一个句子结束符后会句首大写）
            Expect("WOW! ITS AMAZING", "Wow! It's amazing");
        }

        [Test]
        public void Contractions_AposS_Possessive_Its_Negatives()
        {
            // 负例：its 的所有格不应误改为 it's
            // 使用冒号/逗号避免句首强行转为缩写（实现只在 .!? 后切句）
            Expect("THE CAR: ITS TAIL IS LONG.", "The car: its tail is long.");
            Expect("LOOK AT THE ANIMAL, ITS TAIL IS LONG.", "Look at the animal, its tail is long.");
            // 句首 possessive 场景（常见但歧义大）；当前实现句首更倾向缩写，这里避免句首用例
            // 若未来要支持句首 possessive，可新增配置开关并补相应正例
        }

        [Test]
        public void Contractions_AposS_Lets_PositiveAndNegative()
        {
            // 正例：LETS -> Let's
            Expect("LETS GO", "Let's go");
            // 句中 + 常见动词提示
            Expect("OK, LETS TRY AGAIN", "Ok, let's try again");

            // 负例：lets 作为动词三单，保持不变
            Expect("THE ENGINE LETS YOU CONFIGURE OPTIONS", "The engine lets you configure options");
        }

        [Test]
        public void Contractions_AposS_WhWords_ThereHere()
        {
            // what/that/who/where/when/why/how/there/here -> +'s
            Expect("WHATS THIS", "What's this");
            Expect("THATS GREAT", "That's great");
            Expect("WHOS THERE", "Who's there");
            Expect("WHERES THE EXIT", "Where's the exit");
            Expect("THERES A PROBLEM", "There's a problem");
            Expect("HERES THE PLAN", "Here's the plan");
            Expect("HOWS IT GOING", "How's it going");
            Expect("WHENS THE DEADLINE", "When's the deadline");
            Expect("WHYS THAT", "Why's that");
        }

        [Test]
        public void Contractions_AposS_LeadingPunctuation_Quotes()
        {
            // 前导标点/引号不应破坏识别；需要在核心词上判断与改写
            Expect("— ITS AMAZING", "— it's amazing");
            Expect("\"ITS OK\"", "\"It's ok\"");
            Expect("(ITS BAD)", "(It's bad)");
        }

        [Test]
        public void Contractions_AposS_CurlyApostrophe_Preserved()
        {
            // 已带撇号（弯引号）的输入应保持并规范大小写
            Expect("IT’S ALL GOOD", "It’s all good");
            Expect("LET’S GO", "Let’s go");
        }

        [Test]
        public void Idempotence_AposS_RemainsStable()
        {
            var once = EnglishSentenceCaser.ToSentenceCase("ITS OK. WHATS THIS. LETS GO.");
            var twice = EnglishSentenceCaser.ToSentenceCase(once);
            Assert.AreEqual(once, twice, "AposS reconstruction should be idempotent.");
        }

        [Test]
        public void MixedLanguage_AposS_Reconstruction()
        {
            // 中英混合；仅对英文触发缩写修复
            Expect("我们 说 LETS GO NOW", "我们 说 Let's go now");
            Expect("他说 ITS A TEST", "他说 It's a test");
        }
        [Test]
        public void Contractions_AposS_Lets_MixedLanguageAndPhrases()
        {
            // CJK + LETS + verb + phrase recognition
            Expect("我们 说 LETS GO TO NEW YORK CITY", "我们 说 Let's go to New York City");
            // Next token forced lowercase, acronyms preserved
            Expect("他说 LETS USE GPU AND ONNX", "他说 Let's use GPU and ONNX");
        }

        [Test]
        public void Contractions_AposS_Lets_Quotes_Dash_Parens()
        {
            // Quotes, em-dash, parentheses should not break recognition
            Expect("\"LETS GO NOW\"", "\"Let's go now\"");
            Expect("— LETS TRY AGAIN", "— let's try again");
            Expect("LET'S GO—NOW", "Let's go—now");
            Expect("(LETS GO)", "(Let's go)");
        }

        [Test]
        public void Contractions_AposS_Lets_SentenceSequence()
        {
            // Each sentence start can independently reconstruct and capitalize "Let's"
            Expect("LETS GO! LETS TRY AGAIN.", "Let's go! Let's try again.");
        }

        [Test]
        public void Contractions_AposS_Lets_VerbLowercaseOverProperNoun()
        {
            // After "let's", the immediate verb should be lowercase even if it's a brand/proper noun in the lexicon
            Expect("LETS GOOGLE IT", "Let's google it");
        }

        [Test]
        public void Contractions_AposS_Lets_WithAlwaysUpper()
        {
            // Ensure ALWAYS-UPPER tokens still render correctly later in the sentence
            Expect("LET’S USE .NET AND HTTP", "Let’s use .NET and HTTP");
        }

        [Test]
        public void Contractions_AposS_WhWords_Curly()
        {
            // Curly apostrophe variants should be preserved with proper casing
            Expect("WHAT’S UP", "What’s up");
            Expect("HERE’S THE PLAN", "Here’s the plan");
        }

        [Test]
        public void Contractions_AposS_Negatives_PossessiveNames_And_Its()
        {
            // Should not invent possessive apostrophes for arbitrary names
            Expect("DANIELS CAR IS BLUE", "Daniels car is blue");
            // Simple non-sentence-start possessive "its" should remain as "its"
            Expect("IN ITS PLACE", "In its place");
        }

        [Test]
        public void MixedLanguage_AposS_Reconstruction_Additional()
        {
            // Non-Latin boundary capitalization + reconstruction
            Expect("我们 说 LET’S GO", "我们 说 Let’s go");
            Expect("他说 ITS OK", "他说 It's ok");
            Expect("她 说 THATS NICE", "她 说 That's nice");
        }

        [Test]
        public void Idempotence_MixedLanguage_AposS()
        {
            var once = EnglishSentenceCaser.ToSentenceCase("我们 说 LETS GO! 他说 ITS OK.");
            var twice = EnglishSentenceCaser.ToSentenceCase(once);
            Assert.AreEqual(once, twice, "Mixed-language AposS should be idempotent.");
        }
        [Test]
        public void Contractions_AposS_Lets_Negative_LetsUs()
        {
            // Do not reconstruct when "lets" is a 3rd person singular verb
            Expect("HE LETS US CONFIGURE OPTIONS", "He lets us configure options");
            Expect("SOMETIMES SHE LETS US WIN", "Sometimes she lets us win");
        }

        [Test]
        public void Contractions_AposS_Years_ApostropheDecade()
        {
            // Preserve leading apostrophe decade form and normalize case on the S
            Expect("WE LOVE '80S MUSIC", "We love '80s music");
            Expect("'90S GAMES ARE FUN", "'90s games are fun");
        }

        [Test]
        public void Contractions_AposS_Lets_AlwaysUpperAfter()
        {
            // After "Let's", ALWAYS-UPPER tokens must remain uppercase
            Expect("LETS HTTP TEST", "Let's HTTP test");
            Expect("LETS USE C# AND C++", "Let's use C# and C++");
        }

        [Test]
        public void Contractions_AposS_Lets_PhraseAndAcronyms()
        {
            // After "Let's", next verb lowercased; phrase and acronyms respected
            Expect("LETS VISIT NEW YORK CITY WITH GPU", "Let's visit New York City with GPU");
        }

        [Test]
        public void Contractions_AposS_Lets_AfterNumberOrSymbol()
        {
            // Next token is non-letter: ensure we still get overall sensible casing
            Expect("LETS 123 GO", "Let's 123 go");
            Expect("LETS 🚀 GO", "Let's 🚀 go");
        }

        [Test]
        public void Contractions_AposS_WhWords_DoesVariant()
        {
            // what's (does) variant
            Expect("WHATS HE WANT", "What's he want");
        }

        [Test]
        public void Punctuation_LeadingEllipsis_And_Dash_With_Its_Lets()
        {
            // Leading punctuation should not block reconstruction or sentence casing
            Expect("... ITS OK", "... it's ok");
            Expect("— LETS TRY AGAIN", "— let's try again");
        }

        [Test]
        public void NestedQuotes_Parens_KeepReconstruction()
        {
            Expect("(\"LETS GO\")", "(\"Let's go\")");
            Expect("(ITS BAD)", "(It's bad)");
        }

        [Test]
        public void MixedLanguage_Ru_Pure_NoMidSentenceTitleCase()
        {
            // Russian: only sentence start capitalized; mid-sentence tokens stay lower (except proper English items)
            Expect("мы используем OPENAI API сегодня", "Мы используем OpenAI API сегодня");
        }

        [Test]
        public void Idempotence_SecondPass_QuotesAndDash()
        {
            var once = EnglishSentenceCaser.ToSentenceCase("\"LETS GO\" — ITS FINE.");
            var twice = EnglishSentenceCaser.ToSentenceCase(once);
            Assert.AreEqual(once, twice, "Quote/dash reconstruction should be idempotent.");
        }

        [Test]
        public void MixedLanguage_CJK_AposS_And_EnglishFollowups()
        {
            // CJK + Let's with following English verb and acronym
            Expect("她 说 LETS START WITH GPU", "她 说 Let's start with GPU");
        }
    }
}
