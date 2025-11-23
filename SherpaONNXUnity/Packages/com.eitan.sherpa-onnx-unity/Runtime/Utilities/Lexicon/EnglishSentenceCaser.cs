namespace Eitan.SherpaONNXUnity.Runtime.Utilities.Lexicon
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
#if NET8_0_OR_GREATER
    using System.Collections.Frozen;

#endif
#nullable enable

    /// <summary>
    /// Immutable, read-mostly lexicon backing proper-case conversion.
    /// Optimized for very large vocabularies (50k+ singles, 1k+ phrases).
    /// </summary>
    public sealed class ProperCaseLexicon
    {
        // Build-once, cached default lexicon (thread-safe, lazy)
        private static readonly Lazy<ProperCaseLexicon> s_default = new Lazy<ProperCaseLexicon>(() =>
            BuildFromData(
                EnglishProperCaseLexiconData.EnumerateSingles(),
                EnglishProperCaseLexiconData.EnumerateUppers(),
                EnglishProperCaseLexiconData.EnumeratePhrases()
            ));

#if NET8_0_OR_GREATER
        private readonly FrozenDictionary<string, string> _single;
        private readonly FrozenSet<string> _upper;
#else
        // Fallback for older runtimes
        private readonly Dictionary<string, string> _single;
        private readonly HashSet<string> _upper;
#endif

        // Multi-word phrases (greedy) via a token-trie: e.g., "new york" => ["New","York"]
        internal readonly Node _root;

        // Maximum phrase length (in tokens) for bounded lookahead
        public readonly int MaxPhraseLen;

        private ProperCaseLexicon(
#if NET8_0_OR_GREATER
            FrozenDictionary<string, string> single,
            FrozenSet<string> upper,
#else
            Dictionary<string, string> single,
            HashSet<string> upper,
#endif
            Node root,
            int maxPhraseLen)
        {
            _single = single;
            _upper = upper;
            _root = root;
            MaxPhraseLen = Math.Max(1, maxPhraseLen);
        }

        // Returns a cached, build-once default lexicon.
        public static ProperCaseLexicon CreateDefault() => s_default.Value;

        private static ProperCaseLexicon BuildFromData(
            IEnumerable<string> single,
            IEnumerable<string> upper,
            IEnumerable<string> phrases)
        {
            // Pre-size with rough heuristics for big sets
            var singleDict = new Dictionary<string, string>(capacity: 1 << 16, StringComparer.OrdinalIgnoreCase);
            var upperSet = new HashSet<string>(capacity: 1 << 16, StringComparer.OrdinalIgnoreCase);
            var root = new Node();
            int maxPhraseLen = 1;

            // Single-token exact-case entries
            foreach (var s in single)
            {
                if (string.IsNullOrEmpty(s))
                {
                    continue;
                }


                singleDict[s] = s; // exact casing stored as value
            }

            // Always-uppercase entries
            foreach (var u in upper)
            {
                if (string.IsNullOrEmpty(u))
                {
                    continue;
                }


                upperSet.Add(u);
            }

            // Multi-word exact-case phrases
            foreach (var phrase in phrases)
            {
                if (string.IsNullOrWhiteSpace(phrase))
                {
                    continue;
                }


                var tokens = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    continue;
                }


                maxPhraseLen = Math.Max(maxPhraseLen, tokens.Length);

                Node current = root;
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i];
                    if (!current.Children.TryGetValue(token, out Node nextNode))
                    {
                        nextNode = new Node();
                        current.Children[token] = nextNode;
                    }
                    current = nextNode;
                }
                current.Phrase = tokens; // store correctly-cased phrase
            }

#if NET8_0_OR_GREATER
            return new ProperCaseLexicon(singleDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                                         upperSet.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                                         root, maxPhraseLen);
#else
            return new ProperCaseLexicon(singleDict, upperSet, root, maxPhraseLen);
#endif
        }

        /// <summary>Fast contains for upper set.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUpper(string token)
        {
#if NET8_0_OR_GREATER
            return _upper.Contains(token);
#else
            return _upper.Contains(token);
#endif
        }

        /// <summary>Fast try-get for single-word proper case.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSingle(string token, out string proper)
        {
#if NET8_0_OR_GREATER
            return _single.TryGetValue(token, out proper!);
#else
            return _single.TryGetValue(token, out proper!);
#endif
        }

        // Node for the phrase-matching trie. Made internal for access by EnglishSentenceCaser.
        internal class Node
        {
            public readonly Dictionary<string, Node> Children = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            public string[]? Phrase; // Stores the correctly-cased phrase, e.g., ["New", "York"]
        }
    }

    /// <summary>
    /// A high-performance utility to convert all-caps English text to proper sentence case.
    /// Handles sentence boundaries, proper nouns, acronyms, and multi-word phrases
    /// based on a configurable lexicon. Designed for speed with real-time ASR results.
    ///
    /// Perf notes:
    /// - Streaming tokenizer: no array allocations from string.Split.
    /// - Bounded greedy phrase lookahead using lexicon.MaxPhraseLen.
    /// - No temporary punctuation strings; appends slices from source.
    /// - Minimal per-token strings (usually none except when needed for lexicon).
    /// </summary>
    public static class EnglishSentenceCaser
    {
        private static readonly ProperCaseLexicon s_lexicon = ProperCaseLexicon.CreateDefault();
        private static readonly char[] s_sentenceEnders = { '.', '!', '?' };

        // Heuristics for reconstructing missing "'s" in ASR all-caps (e.g., ITS -> It's, WHATS -> What's)
        private static readonly HashSet<string> s_AposSWhitelist = new HashSet<string>(new[]
        {
            "it","that","what","who","where","when","why","how","there","here","let"
        }, StringComparer.OrdinalIgnoreCase);

        // Hints that "its" is likely a contraction ("it's ...") rather than a possessive.
        private static readonly HashSet<string> s_ItsNextHints = new HashSet<string>(new[]
        {
            "a","an","the","not","never","no","got","been","being","going","gonna","ok","okay","fine",
            "so","too","very","just","probably","definitely","already","also","almost","only","ever","even"
        }, StringComparer.OrdinalIgnoreCase);

        // Common verb-y next words after "let's ..."
        private static readonly HashSet<string> s_LetsNextVerbs = new HashSet<string>(new[]
        {
            // Core
            "go","get","see","try","do","use","start","begin","move","keep","take","make","play","eat","talk","run","walk",
            "build","test","check","learn","read","write","watch","listen","work","study","create","design","debug","optimize","improve","fix",
            "open","close","show","hide","add","remove","delete","update","upgrade","install","uninstall","reset","restart","reboot",
            "call","meet","discuss","plan","prepare","review","ship","launch","deploy","release","commit","push","pull","merge",
            "train","tune","measure","benchmark","profile","compile","render","record","capture","save","load","export","import","analyze",
            "build","pack","publish","generate","synthesize","simulate","test","validate","verify","evaluate"
        }, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Converts an all-uppercase string to sentence case according to English grammar rules.
        /// Designed to run in real-time on ASR outputs with massive lexicons.
        /// </summary>
#if NET6_0_OR_GREATER || NET7_0_OR_GREATER || NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static string ToSentenceCase(string allCapsText)
        {
            if (string.IsNullOrEmpty(allCapsText))
            {
                return string.Empty;
            }

            var lx = s_lexicon;
            var sb = new StringBuilder(allCapsText.Length);
            bool capitalizeNext = true;
            bool lastWasLets = false;

            // Fixed-size lookahead buffer to avoid List.RemoveAt/RemoveRange churn
            int capacity = lx.MaxPhraseLen;
            if (capacity < 1)
            {
                capacity = 1;
            }

            var buffer = new Token[capacity];
            int bufferCount = 0;

            int idx = 0;
            bool needSpace = false;

            // Prime the lookahead buffer
            FillLookahead(allCapsText, capacity, ref idx, buffer, ref bufferCount);

            while (bufferCount > 0)
            {
                // Attempt a greedy phrase match from the lookahead buffer
                var (phrase, consumed) = MatchPhraseFromBuffer(allCapsText, buffer, bufferCount, lx._root);

                if (phrase != null && consumed > 0)
                {
                    // Emit phrase tokens
                    for (int j = 0; j < consumed; j++)
                    {
                        var tok = buffer[j];
                        if (needSpace)
                        {
                            sb.Append(' ');
                        }
                        needSpace = true;
                        // Use the lexicon's exact-cased phrase tokens
                        sb.Append(phrase[j]);
                        // Append original trailing punctuation (if any) directly from source
                        if (tok.PuncLen > 0)
                        {
                            sb.Append(allCapsText, tok.PuncStart, tok.PuncLen);
                        }
                    }
                    // Update capitalization state from the last token emitted
                    var lastTok = buffer[consumed - 1];
                    capitalizeNext = lastTok.HasSentenceEnder;
                    // Phrase emission clears "let's" context (we only special-case the immediate next token).
                    lastWasLets = false;
                    // Consume tokens from the front by shifting the window
                    ShiftLeft(buffer, consumed, ref bufferCount);
                    // Refill lookahead
                    FillLookahead(allCapsText, capacity - bufferCount, ref idx, buffer, ref bufferCount);
                    continue;
                }

                // No phrase match; emit the first token with single-word rules
                var t = buffer[0];
                if (needSpace)
                {
                    sb.Append(' ');
                }
                needSpace = true;
                EmitSingleToken(allCapsText, t, sb, lx, ref capitalizeNext, ref lastWasLets, bufferCount > 1, bufferCount > 1 ? buffer[1] : default);
                // Consume one token and refill
                ShiftLeft(buffer, 1, ref bufferCount);
                FillLookahead(allCapsText, capacity - bufferCount, ref idx, buffer, ref bufferCount);
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ShiftLeft(Token[] buffer, int count, ref int bufferCount)
        {
            if (count <= 0)
            {
                return;
            }


            int remaining = bufferCount - count;
            if (remaining > 0)
            {
                Array.Copy(buffer, count, buffer, 0, remaining);
            }


            bufferCount = Math.Max(remaining, 0);
        }

        // Detect if the previous non-space character is a *non-Latin* letter (e.g., CJK),
        // in which case we treat the next English token like the start of a segment and capitalize it.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLatinLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldCapitalizeAfterNonLatin(string source, int wordStart)
        {
            int i = wordStart - 1;
            // Skip spaces
            while (i >= 0 && source[i] == ' ')
            {
                i--;
            }
            if (i < 0)
            {
                return false;
            }

            char prev = source[i];
            // If previous char is Latin letter/digit or apostrophe, do not force-capitalize
            if (IsLatinLetter(prev) || char.IsDigit(prev) || prev == '\'' || prev == '’')
            {
                return false;
            }

            // If previous is a letter but not Latin (e.g., CJK), treat as new English segment
            return char.IsLetter(prev) && !IsLatinLetter(prev);
        }

        // Emit a single token using Single/Upper lexicons and sentence-capitalization rules
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsLower(ReadOnlySpan<char> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (char.IsLower(span[i]))
                {
                    return true;
                }

            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLetsTokenCore(ReadOnlySpan<char> core)
        {
            if (core.Length != 5)
            {
                return false;
            }


            char c0 = core[0], c1 = core[1], c2 = core[2], c3 = core[3], c4 = core[4];
            return ((c0 == 'l' || c0 == 'L') &&
                    (c1 == 'e' || c1 == 'E') &&
                    (c2 == 't' || c2 == 'T') &&
                    (c3 == '\'' || c3 == '’') &&
                    (c4 == 's' || c4 == 'S'));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (bool IsLatinWord, string Lower) GetPrevLatinWordLower(string source, int wordStart)
        {
            int i = wordStart - 1;
            // Skip spaces
            while (i >= 0 && source[i] == ' ')
            {
                i--;
            }

            if (i < 0)
            {
                return (false, string.Empty);
            }

            // Find the start of previous non-space run

            int end = i + 1; // exclusive
            int start = i;
            while (start >= 0 && source[start] != ' ')
            {
                start--;
            }


            start++;

            // Split trailing punctuation for the previous run
            int last = end - 1;
            while (last >= start)
            {
                char c = source[last];
                if (char.IsLetterOrDigit(c) || c == '\'' || c == '’')
                {
                    break;
                }


                last--;
            }
            if (last < start)
            {
                return (false, string.Empty);
            }

            // Now [start, last] is the "core" of the previous token

            bool hasLatin = false;
            for (int k = start; k <= last; k++)
            {
                char c = source[k];
                if (IsLatinLetter(c)) { hasLatin = true; break; }
            }
            string lower = source.AsSpan(start, last - start + 1).ToString().ToLowerInvariant();
            return (hasLatin, lower);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EmitSingleToken(string source, Token t, StringBuilder sb, ProperCaseLexicon lx, ref bool capitalizeNext, ref bool lastWasLets, bool hasNext, Token nextTok)
        {
            // Slice for the full token (no allocation)
            ReadOnlySpan<char> wordSpan = source.AsSpan(t.WordStart, t.WordLen);

            // If previous token was "let's", force this token to be lowercased (verb form),
            // ignoring Single/Upper lexicon matches like the programming language "Go".
            if (lastWasLets)
            {
                // Identify leading punctuation so we preserve it.
                int leadTmp = 0;
                while (leadTmp < wordSpan.Length)
                {
                    char c = wordSpan[leadTmp];
                    if (char.IsLetterOrDigit(c) || c == '\'' || c == '’')
                    {
                        break;
                    }
                    leadTmp++;
                }

                ReadOnlySpan<char> coreTmp = wordSpan.Slice(leadTmp);

                string wordStringTmp = wordSpan.ToString();
                // Consider trailing punctuation combos like C# / C++ as a single ALWAYS-UPPER token
                string? puncStrTmp = t.PuncLen > 0 ? source.AsSpan(t.PuncStart, t.PuncLen).ToString() : null;
                bool isAlwaysUpper = lx.IsUpper(wordStringTmp);
                if (!isAlwaysUpper && !string.IsNullOrEmpty(puncStrTmp))
                {
                    if (puncStrTmp == "#" || puncStrTmp == "++")
                    {
                        var combined = wordStringTmp + puncStrTmp;
                        if (lx.IsUpper(combined))
                        {
                            isAlwaysUpper = true;
                        }
                    }
                }
                bool coreStartsWithLetter = coreTmp.Length > 0 && char.IsLetter(coreTmp[0]);

                // If the next token is non-letter (e.g., 123, emoji), emit it and KEEP the "let's" context for the following token.
                if (!coreStartsWithLetter)
                {
                    // Emit token as-is (word + trailing punctuation)
                    sb.Append(source, t.WordStart, t.WordLen);
                    if (t.PuncLen > 0)
                    {
                        sb.Append(source, t.PuncStart, t.PuncLen);
                    }
                    capitalizeNext = t.HasSentenceEnder;
                    // Do NOT clear lastWasLets — we want to lower the next letter-starting token.
                    return;
                }

                // If not ALWAYS-UPPER and starts with a letter, force-lowercase this token (verb-ish) and clear context.
                if (!isAlwaysUpper)
                {
                    if (leadTmp > 0)
                    {
                        sb.Append(source, t.WordStart, leadTmp);
                    }
                    for (int k = 0; k < coreTmp.Length; k++)
                    {
                        sb.Append(char.ToLowerInvariant(coreTmp[k]));
                    }
                    if (t.PuncLen > 0)
                    {
                        sb.Append(source, t.PuncStart, t.PuncLen);
                    }
                    capitalizeNext = t.HasSentenceEnder;
                    lastWasLets = false;
                    return;
                }

                // Otherwise it's ALWAYS-UPPER; fall through to normal processing and CLEAR the context (only immediate token is affected).
                lastWasLets = false;
            }

            // Identify leading punctuation so we can capitalize the first alphabetic after it.
            int lead = 0;
            while (lead < wordSpan.Length)
            {
                char c = wordSpan[lead];
                if (char.IsLetterOrDigit(c))
                {
                    break;
                }
                // Keep apostrophe as part of the core for cases like '80s (rare in ASR); treat opening quotes/parens as leading.
                if (c == '\'' || c == '’')
                {
                    break;
                }
                lead++;
            }

            ReadOnlySpan<char> coreSpan = wordSpan.Slice(lead);
            bool coreStartsLatin = coreSpan.Length > 0 && IsLatinLetter(coreSpan[0]);
            bool segmentStart = capitalizeNext || (coreStartsLatin && ShouldCapitalizeAfterNonLatin(source, t.WordStart));

            string? wordString = null; // full token string (may include leading punctuation)
            string? coreString = null;  // token without leading punctuation
            bool emittedLets = false;

            // Treat the start of an English segment (sentence start or after non‑Latin) as a capitalization trigger.
            // (defined above as `segmentStart`)

            // Always‑upper items (e.g., ".NET", HTTP) — use the full token so leading punctuation is honored.
            wordString ??= wordSpan.ToString();
            bool alwaysUpperHit = lx.IsUpper(wordString);
            if (!alwaysUpperHit && t.PuncLen > 0)
            {
                string puncStr = source.AsSpan(t.PuncStart, t.PuncLen).ToString();
                if (puncStr == "#" || puncStr == "++")
                {
                    var combined = wordString + puncStr; // e.g., "C#", "C++"
                    if (lx.IsUpper(combined))
                    {
                        alwaysUpperHit = true;
                    }
                }
            }
            if (alwaysUpperHit)
            {
                sb.Append(wordString.ToUpperInvariant()); // trailing punctuation appended later
                goto AppendPuncAndFinish;
            }

            // Proper‑cased single tokens (e.g., "OpenAI", "GitHub").
            if (coreSpan.Length > 0)
            {
                coreString ??= coreSpan.ToString();
                if (lx.TryGetSingle(coreString, out var proper))
                {
                    if (lead > 0)
                    {
                        sb.Append(source, t.WordStart, lead);
                    }


                    sb.Append(proper);
                    goto AppendPuncAndFinish;
                }
            }

            // Single‑letter pronoun "I" at any position (no leading punctuation)
            if (coreSpan.Length == 1 && (coreSpan[0] == 'I' || coreSpan[0] == 'i') && lead == 0)
            {
                sb.Append('I');
                goto AppendPuncAndFinish;
            }

            // Keep "I'm/I've/I'd/I'll" with capital I even mid‑sentence (ASCII or curly apostrophe)
            if (coreSpan.Length >= 2 && (coreSpan[0] == 'I' || coreSpan[0] == 'i') && (coreSpan[1] == '\'' || coreSpan[1] == '’'))
            {
                if (lead > 0)
                {
                    sb.Append(source, t.WordStart, lead);
                }


                sb.Append('I');
                for (int k = 1; k < coreSpan.Length; k++)
                {
                    sb.Append(char.ToLowerInvariant(coreSpan[k]));
                }


                goto AppendPuncAndFinish;
            }

            // ---------- Heuristic reconstruction of missing "'s" (contractions like it's / what's / let's) ----------
            // Only attempt if there is no apostrophe in the core and it ends with 's' (case-insensitive).
            bool coreHasApostrophe = coreSpan.IndexOf('\'') >= 0 || coreSpan.IndexOf('’') >= 0;
            if (!coreHasApostrophe && coreSpan.Length >= 2)
            {
                char last = coreSpan[coreSpan.Length - 1];
                if (last == 's' || last == 'S')
                {
                    ReadOnlySpan<char> baseSpan = coreSpan.Slice(0, coreSpan.Length - 1);
                    string baseLower = baseSpan.ToString().ToLowerInvariant();

                    if (s_AposSWhitelist.Contains(baseLower))
                    {
                        bool ok = true;

                        // Special handling for ambiguous cases:
                        if (baseLower == "it")
                        {
                            // Favor "it's" at sentence start or when next token hints at a predicate/adjective/aux.
                            ok = segmentStart;

                            if (!ok && hasNext)
                            {
                                // Extract next token's core (without leading punctuation).
                                ReadOnlySpan<char> nSpan = source.AsSpan(nextTok.WordStart, nextTok.WordLen);
                                int nLead = 0;
                                while (nLead < nSpan.Length)
                                {
                                    char c = nSpan[nLead];
                                    if (char.IsLetterOrDigit(c) || c == '\'' || c == '’')
                                    {
                                        break;
                                    }
                                    nLead++;
                                }
                                ReadOnlySpan<char> nCore = nSpan.Slice(nLead);
                                if (!nCore.IsEmpty)
                                {
                                    string nextLower = nCore.ToString().ToLowerInvariant();
                                    ok = s_ItsNextHints.Contains(nextLower) || nextLower.EndsWith("ing", StringComparison.Ordinal);
                                }
                            }
                        }
                        else if (baseLower == "let")
                        {
                            // Prefer the contraction at sentence/segment start, or when the next token looks like a verb.
                            ok = segmentStart;

                            // Look ahead
                            if (hasNext && !ok)
                            {
                                ReadOnlySpan<char> nSpan = source.AsSpan(nextTok.WordStart, nextTok.WordLen);
                                int nLead = 0;
                                while (nLead < nSpan.Length)
                                {
                                    char c = nSpan[nLead];
                                    if (char.IsLetterOrDigit(c) || c == '\'' || c == '’')
                                    {
                                        break;
                                    }


                                    nLead++;
                                }
                                ReadOnlySpan<char> nCore = nSpan.Slice(nLead);
                                if (!nCore.IsEmpty)
                                {
                                    string nextLower = nCore.ToString().ToLowerInvariant();
                                    // Heuristic: base-form verb list or common -ing continuation.
                                    ok = s_LetsNextVerbs.Contains(nextLower) || nextLower.EndsWith("ing", StringComparison.Ordinal);
                                }
                            }

                            // Look behind to avoid false positives like "He lets us ..."
                            if (ok)
                            {
                                var (prevIsLatin, prevLower) = GetPrevLatinWordLower(source, t.WordStart);
                                if (prevIsLatin && hasNext)
                                {
                                    // If the next word is "us" and there's a Latin subject before, it's likely "lets us" (3rd person), not "let's".
                                    ReadOnlySpan<char> nSpan2 = source.AsSpan(nextTok.WordStart, nextTok.WordLen);
                                    int nLead2 = 0;
                                    while (nLead2 < nSpan2.Length)
                                    {
                                        char c = nSpan2[nLead2];
                                        if (char.IsLetterOrDigit(c) || c == '\'' || c == '’')
                                        {
                                            break;
                                        }


                                        nLead2++;
                                    }
                                    ReadOnlySpan<char> nCore2 = nSpan2.Slice(nLead2);
                                    if (!nCore2.IsEmpty)
                                    {
                                        string nextLower2 = nCore2.ToString().ToLowerInvariant();
                                        if (nextLower2 == "us")
                                        {
                                            ok = false;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // For "what/that/who/where/when/why/how/there/here" we almost always want the contraction.
                            ok = true;
                        }

                        if (ok)
                        {
                            if (lead > 0)
                            {
                                sb.Append(source, t.WordStart, lead);
                            }

                            // Emit base with proper casing. Capitalize at sentence start OR when starting an English
                            // segment immediately after a non-Latin token (e.g., CJK + space + LETS -> "Let's").
                            bool capThis = segmentStart;
                            if (baseSpan.Length > 0)
                            {
                                // First letter
                                sb.Append(capThis ? char.ToUpperInvariant(baseSpan[0]) : char.ToLowerInvariant(baseSpan[0]));
                                // Rest lower
                                for (int k = 1; k < baseSpan.Length; k++)
                                {
                                    sb.Append(char.ToLowerInvariant(baseSpan[k]));
                                }
                            }
                            sb.Append("'s");
                            emittedLets = baseLower == "let";
                            goto AppendPuncAndFinish;
                        }
                    }
                }
            }
            // ---------- End "'s" reconstruction ----------

            // Preserve already‑mixed case when not at a segment start and not matched above.
            if (ContainsLower(wordSpan) && !segmentStart)
            {
                sb.Append(source, t.WordStart, t.WordLen);
                // If the core token is exactly "let’s"/"let's", propagate the lowering context.
                if (IsLetsTokenCore(coreSpan))
                {
                    emittedLets = true;
                }
                goto AppendPuncAndFinish;
            }

            // General casing:
            if (segmentStart && coreSpan.Length > 0)
            {
                // Preserve leading punctuation, capitalize the first alphabetic char, lower the rest
                if (lead > 0)
                {
                    sb.Append(source, t.WordStart, lead);
                }
                // Capitalize first char of core
                sb.Append(char.ToUpperInvariant(coreSpan[0]));
                for (int k = 1; k < coreSpan.Length; k++)
                {
                    sb.Append(char.ToLowerInvariant(coreSpan[k]));
                }
                // If this token is "let's" (ASCII or curly apostrophe), mark for lowering the next token.
                if (IsLetsTokenCore(coreSpan))
                {
                    emittedLets = true;
                }
            }
            else
            {
                // Lowercase core while preserving any leading punctuation
                if (lead > 0)
                {
                    sb.Append(source, t.WordStart, lead);
                }
                for (int k = 0; k < coreSpan.Length; k++)
                {
                    sb.Append(char.ToLowerInvariant(coreSpan[k]));
                }
                // If this token is "let's" (ASCII or curly apostrophe), mark for lowering the next token.
                if (IsLetsTokenCore(coreSpan))
                {
                    emittedLets = true;
                }
            }

        AppendPuncAndFinish:
            // Append trailing punctuation from the source without creating a string
            if (t.PuncLen > 0)
            {
                sb.Append(source, t.PuncStart, t.PuncLen);
            }

            // Update sentence-capitalization flag
            capitalizeNext = t.HasSentenceEnder;
            // Update "let's" context for the next token.
            if (emittedLets)
            {
                lastWasLets = true;
            }
            else
            {
                // Clear unless explicitly set by this token.
                lastWasLets = false;
            }
        }

        /// <summary>
        /// Greedily matches the longest phrase from the lexicon starting at buffer[0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (string[]? Phrase, int TokensConsumed) MatchPhraseFromBuffer(string src, Token[] buffer, int bufferCount, ProperCaseLexicon.Node root)
        {
            var current = root;
            (string[]? Phrase, int TokensConsumed) longest = (null, 0);

            for (int i = 0; i < bufferCount; i++)
            {
                var tok = buffer[i];
                var span = src.AsSpan(tok.WordStart, tok.WordLen);

                // We must compare with dictionary keys (strings). Convert once.
                string key = span.ToString();

                if (!current.Children.TryGetValue(key, out var next))
                {
                    break;
                }


                current = next;
                if (current.Phrase != null)
                {
                    longest = (current.Phrase, i + 1);
                }
            }
            return longest;
        }

        /// <summary>
        /// Tokenizes ahead up to 'want' tokens and appends into the lookahead buffer.
        /// A "token" is a contiguous non-space run; we split that run into (word, trailing punctuation).
        /// Leading punctuation (e.g. ".NET") stays with the word so acronyms like ".NET" are preserved.
        /// </summary>
#if NET6_0_OR_GREATER || NET7_0_OR_GREATER || NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static void FillLookahead(string src, int want, ref int idx, Token[] buffer, ref int bufferCount)
        {
            int n = src.Length;
            while (want > 0)
            {
                // Skip spaces
                while (idx < n && src[idx] == ' ')
                {
                    idx++;
                }

                if (idx >= n)
                {
                    break;
                }

                int start = idx;
                // Move to end of this non-space run
                while (idx < n && src[idx] != ' ')
                {
                    idx++;
                }


                int end = idx; // exclusive

                // Split trailing punctuation
                int last = end - 1;
                while (last >= start)
                {
                    char c = src[last];
                    if (char.IsLetterOrDigit(c) || c == '\'')
                    {
                        break;
                    }


                    last--;
                }

                int wordStart, wordLen, puncStart = 0, puncLen = 0;

                if (last < start)
                {
                    // All punctuation (treat as word so we still emit it)
                    wordStart = start;
                    wordLen = end - start;
                }
                else
                {
                    wordStart = start;
                    wordLen = (last - start + 1);
                    puncStart = last + 1;
                    puncLen = end - puncStart;
                }

                bool hasEnder = false;
                if (puncLen > 0)
                {
                    // Check if punctuation contains a sentence ender
                    var puncSpan = src.AsSpan(puncStart, puncLen);
                    hasEnder = puncSpan.IndexOfAny(s_sentenceEnders) >= 0;
                }

                buffer[bufferCount++] = new Token(wordStart, wordLen, puncStart, puncLen, hasEnder);
                want--;
                if (bufferCount == buffer.Length)
                {
                    break;
                }

            }
        }

        // Compact token representation used for lookahead and emission (no string allocations)
        private readonly struct Token
        {
            public readonly int WordStart;
            public readonly int WordLen;
            public readonly int PuncStart;
            public readonly int PuncLen;
            public readonly bool HasSentenceEnder;

            public Token(int wordStart, int wordLen, int puncStart, int puncLen, bool hasSentenceEnder)
            {
                WordStart = wordStart;
                WordLen = wordLen;
                PuncStart = puncStart;
                PuncLen = puncLen;
                HasSentenceEnder = hasSentenceEnder;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Maintainer-owned data (pure-code). Extend these providers to 50,000+ entries.
    // This class is referenced only by ProperCaseLexicon.CreateDefault().
    //
    // IMPORTANT: You can add your own file `EnglishProperCaseLexiconExtra.cs` in the
    // same namespace with:
    //
    //   internal static class EnglishProperCaseLexiconExtra {
    //       public static readonly string[] Single  = { "Haisheng", "Haibao", ... };
    //       public static readonly string[] Upper   = { "EOU", "VAD", "GPU", ... };
    //       public static readonly string[] Phrases = { "SherpaONNX", "ZipVoice TTS", ... };
    //   }
    //
    // Those arrays will be automatically concatenated via reflection at startup.
    // -------------------------------------------------------------------------
    internal static class EnglishProperCaseLexiconData
    {
        // Baseline list of single-token proper nouns, brands, places, titles, holidays, adjectives.
        private static readonly string[] Single = new string[]
        {
            // Days and Months
            "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday",
            "January","February","March","April","May","June","July","August","September","October","November","December",

            // Holidays
            "Christmas","Thanksgiving","Easter","Hanukkah","Diwali","Ramadan","Passover","Halloween",
            "Lunar New Year","New Year's Day","Labor Day","Independence Day",

            // Titles and honorifics (common)
            "Mr","Mrs","Ms","Miss","Dr","Prof","Sir","Madam","Dame","Lord","Lady",
            "President","Governor","Mayor","Captain","Officer","Judge","Senator","Representative","General",

            // Family terms (when used as titles before names; conservative inclusion)
            "Mother","Father","Mom","Dad","Aunt","Uncle","Grandma","Grandpa",

            // Nationalities / proper adjectives
            "American","British","Chinese","Japanese","Korean","German","French","Italian","Spanish","Portuguese",
            "Russian","Ukrainian","Indian","Thai","Vietnamese","Indonesian","Malaysian","Singaporean","Taiwanese",
            "Turkish","Greek","Polish","Dutch","Swedish","Norwegian","Finnish","Danish","Swiss","Austrian","Canadian","Australian",

            // Tech Companies & Brands
            "Google","Microsoft","Apple","OpenAI","NVIDIA","AMD","Intel","GitHub","Unity","Unreal","TensorFlow","PyTorch",
            "Amazon","Facebook","Meta","Netflix","Tesla","Oracle","IBM","Samsung","Sony","Adobe","Salesforce","SAP",
            "Qualcomm","Broadcom","Cisco","Zoom","Twitter","SpaceX","Palantir","Snowflake","Databricks","HuggingFace",
            "Huawei","Xiaomi","Lenovo","Dell","HP","Asus","Acer","MSI","Logitech","Razer","Corsair",

            // Software & Libraries
            "Windows","Linux","macOS","Android","iOS","iPadOS","watchOS","tvOS","Ubuntu","Debian","Fedora","Arch","CentOS","Alpine",
            "React","Vue","Angular","Svelte","Next.js","Nuxt","Electron","Vite","PostgreSQL","MySQL","MongoDB","SQLite","Redis","Kafka",
            "ChatGPT","Sora","ZipVoice","Sherpa","ONNX","OpenCV","NumPy","pandas","SciPy","JAX","Matplotlib","TensorRT","cuDNN",
            "Java","JavaScript","Python","CSharp","C++","Ruby","Go","Swift","Kotlin","TypeScript","PHP","Perl","Rust","Scala","Haskell","Lua",
            "Docker","Kubernetes","Terraform","Ansible","Jenkins","Git","Subversion","Jira","Confluence","Slack","Trello","Figma",
            "Photoshop","Illustrator","Premiere","Blender","Maya","Cinema4D","Godot","CryEngine","Spark","Hadoop","Elasticsearch",
            "Excel","Word","PowerPoint","Outlook","Teams","SharePoint","DynamoDB","Firestore","BigQuery","Redshift",

            // Geography (Countries & Major Cities)
            "Afghanistan","Albania","Algeria","Andorra","Angola","Argentina","Armenia","Australia","Austria","Azerbaijan",
            "Belgium","Brazil","Canada","China","Colombia","Czechia","Denmark","Egypt","Finland","France","Germany","Greece","Hungary",
            "India","Indonesia","Iran","Iraq","Ireland","Israel","Italy","Japan","Kenya","Luxembourg","Malaysia","Mexico","Morocco",
            "Netherlands","New Zealand","Norway","Pakistan","Peru","Philippines","Poland","Portugal","Qatar","Romania","Russia",
            "Saudi Arabia","Singapore","South Africa","South Korea","Spain","Sweden","Switzerland","Taiwan","Thailand","Turkey",
            "Ukraine","United Kingdom","United States","Vietnam","Zimbabwe",
            "London","Paris","Tokyo","Beijing","Shanghai","Moscow","Cairo","Istanbul","Bangkok","Singapore","Dubai","Sydney","Melbourne",
            "Toronto","Vancouver","Montreal","Berlin","Madrid","Barcelona","Rome","Milan","Athens","Mumbai","Delhi","Kolkata","Lagos",

            // Common English Names (subset)
            "James","John","Robert","Michael","William","David","Richard","Joseph","Thomas","Charles",
            "Mary","Patricia","Jennifer","Linda","Elizabeth","Barbara","Susan","Jessica","Sarah","Karen",
            "Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez",
            "Lee","Walker","Hall","Allen","Young","Hernandez","King","Wright","Lopez","Hill",
        };

        // Acronyms and initialisms
        private static readonly string[] Upper = new string[]
        {
            // Tech & Computing
            ".NET","C#","C++",
            "CPU","GPU","APU","RAM","ROM","SSD","HDD","API","HTTP","HTTPS","TCP","UDP","TLS","SSL","SSH","DNS","DHCP","MQTT","AMQP",
            "JSON","XML","YAML","CSV","TSV","HTML","CSS","SVG","WASM","SDK","IDE","USB","VGA","HDMI","REST","SOAP","RPC","GRPC","JWT",
            "AI","ML","DL","NLP","CV","RL","LLM","ASR","TTS","VAD","VITS","DSP","BERT","GPT","T5","GAN","CNN","RNN","LSTM",
            "IL2CPP","AOT","JIT","CLR","SIMD","SSE2","AVX","AVX2","AV1","H.264","H.265","HDR","HDR10","SDR","VR","AR","XR","MR",
            "PDF","PNG","JPG","JPEG","GIF","TIFF","WEBP","WAV","MP3","OGG","OPUS","FLAC","AAC","PCM","MIDI",
            "URL","URI","UUID","GUID","CRUD","SSO","SAML","OAUTH","CI","CD","AGP","BIOS","DVD","SATA","SCSI",
            // Business & Finance
            "CEO","CFO","CTO","COO","CIO","CMO","HR","PR","R&D","QA","QC","IPO","ROI","KPI","VAT","NASDAQ","NYSE","S&P",
            "USD","EUR","GBP","JPY","TWD","CNY","KRW","CAD","AUD","CHF","INR",
            // Organizations & Media
            "USA","UK","EU","UN","NATO","NASA","FBI","CIA","NSA","IRS","UNESCO","UNICEF","WHO","WWF","NPR","BBC","CNN","ESPN","HBO","UFC",
            // Unity/Audio ASR/TTS specific
            "EOU","VAD","ASIO","VST","GC","P/INVOKE","JNI",
        };

        // Multi-word phrases (case-insensitive match; greedy)
        private static readonly string[] Phrases = new string[]
        {
            // Geography
            "San Francisco","New York","New York City","Los Angeles","Silicon Valley",
            "United States","United Kingdom","South Korea","North Korea","New Zealand","Saudi Arabia","South Africa","United Arab Emirates",
            "San Diego","San Jose","Las Vegas","Washington DC","Hong Kong","Rio de Janeiro","Buenos Aires",

            // Companies & Products
            "Visual Studio Code","JetBrains Rider","Google Cloud","Microsoft Azure","Amazon Web Services",
            "OpenAI API","Google BigQuery","Kafka Streams","Kafka Connect",
            "Google Chrome","Mozilla Firefox","Microsoft Edge","Internet Explorer","Microsoft Office","Google Docs","Google Sheets","Google Slides",
            "Adobe Creative Cloud","Final Cut Pro","Ableton Live","Pro Tools",

            // Concepts & Titles
            "Stochastic Gradient Descent","Zero Shot Learning","Few Shot Learning","Large Language Model",
            "Unity Editor","Unity Player","SherpaONNX","Visual Studio",
            "World War I","World War II","Cold War","Middle Ages","Renaissance Period","Industrial Revolution","Information Age",
            "Artificial Intelligence","Machine Learning","Deep Learning","Natural Language Processing","Computer Vision",
            "Augmented Reality","Virtual Reality","Mixed Reality","Internet of Things","Big Data","Cloud Computing","Quantum Computing",
            "User Interface","User Experience","Object Oriented Programming","Functional Programming","Agile Development",
            "Chief Executive Officer","Chief Financial Officer","Chief Technology Officer","Vice President",
            "Standard And Poor's","Dow Jones Industrial Average",
        };

        // --- Public enumerators that auto-append optional "Extra" shards via reflection ---

        public static IEnumerable<string> EnumerateSingles()
        {
            foreach (var s in Single)
            {
                yield return s;
            }


            foreach (var s in GetExtra("Single"))
            {
                yield return s;
            }

        }

        public static IEnumerable<string> EnumerateUppers()
        {
            foreach (var s in Upper)
            {
                yield return s;
            }


            foreach (var s in GetExtra("Upper"))
            {
                yield return s;
            }

        }

        public static IEnumerable<string> EnumeratePhrases()
        {
            foreach (var s in Phrases)
            {
                yield return s;
            }


            foreach (var s in GetExtra("Phrases"))
            {
                yield return s;
            }

        }

        private static IEnumerable<string> GetExtra(string fieldName)
        {
            // Optional extension hook: define EnglishProperCaseLexiconExtra with matching string[] fields.
            IEnumerable<string>? extra = null;
            try
            {
                var t = Type.GetType("Eitan.SherpaONNXUnity.Runtime.Utilities.Lexicon.EnglishProperCaseLexiconExtra");
                if (t != null)
                {
                    var f = t.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (f != null)
                    {
                        extra = f.GetValue(null) as IEnumerable<string>;
                    }
                }
            }
            catch
            {
                extra = null;
            }

            if (extra != null)
            {
                foreach (var s in extra)
                {
                    if (!string.IsNullOrEmpty(s))
                    {

                        yield return s;
                    }

                }
            }
        }
    }
}
