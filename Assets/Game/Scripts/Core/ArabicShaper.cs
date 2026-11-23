using System.Collections.Generic;
using System.Text;

namespace StreetRush
{
    /// <summary>
    /// Minimal Arabic shaper + bidi reordering for uGUI's left-to-right text
    /// renderer: converts letters to their contextual Presentation Forms-B
    /// glyphs (isolated/final/initial/medial, plus lam-alef ligatures) and
    /// reverses right-to-left runs while keeping Latin/digit runs readable.
    /// Text without Arabic characters is returned unchanged.
    /// </summary>
    public static class ArabicShaper
    {
        // [isolated, final, initial, medial]; 0 = form doesn't exist
        static readonly Dictionary<char, ushort[]> Forms = new Dictionary<char, ushort[]>
        {
            { 'ء', new ushort[] { 0xFE80, 0, 0, 0 } },
            { 'آ', new ushort[] { 0xFE81, 0xFE82, 0, 0 } },
            { 'أ', new ushort[] { 0xFE83, 0xFE84, 0, 0 } },
            { 'ؤ', new ushort[] { 0xFE85, 0xFE86, 0, 0 } },
            { 'إ', new ushort[] { 0xFE87, 0xFE88, 0, 0 } },
            { 'ئ', new ushort[] { 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C } },
            { 'ا', new ushort[] { 0xFE8D, 0xFE8E, 0, 0 } },
            { 'ب', new ushort[] { 0xFE8F, 0xFE90, 0xFE91, 0xFE92 } },
            { 'ة', new ushort[] { 0xFE93, 0xFE94, 0, 0 } },
            { 'ت', new ushort[] { 0xFE95, 0xFE96, 0xFE97, 0xFE98 } },
            { 'ث', new ushort[] { 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C } },
            { 'ج', new ushort[] { 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 } },
            { 'ح', new ushort[] { 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 } },
            { 'خ', new ushort[] { 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 } },
            { 'د', new ushort[] { 0xFEA9, 0xFEAA, 0, 0 } },
            { 'ذ', new ushort[] { 0xFEAB, 0xFEAC, 0, 0 } },
            { 'ر', new ushort[] { 0xFEAD, 0xFEAE, 0, 0 } },
            { 'ز', new ushort[] { 0xFEAF, 0xFEB0, 0, 0 } },
            { 'س', new ushort[] { 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 } },
            { 'ش', new ushort[] { 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 } },
            { 'ص', new ushort[] { 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC } },
            { 'ض', new ushort[] { 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 } },
            { 'ط', new ushort[] { 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 } },
            { 'ظ', new ushort[] { 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 } },
            { 'ع', new ushort[] { 0xFEC9, 0xFECA, 0xFECB, 0xFECC } },
            { 'غ', new ushort[] { 0xFECD, 0xFECE, 0xFECF, 0xFED0 } },
            { 'ف', new ushort[] { 0xFED1, 0xFED2, 0xFED3, 0xFED4 } },
            { 'ق', new ushort[] { 0xFED5, 0xFED6, 0xFED7, 0xFED8 } },
            { 'ك', new ushort[] { 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC } },
            { 'ل', new ushort[] { 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 } },
            { 'م', new ushort[] { 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 } },
            { 'ن', new ushort[] { 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 } },
            { 'ه', new ushort[] { 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC } },
            { 'و', new ushort[] { 0xFEED, 0xFEEE, 0, 0 } },
            { 'ى', new ushort[] { 0xFEEF, 0xFEF0, 0, 0 } },
            { 'ي', new ushort[] { 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 } },
        };

        // lam + alef variant -> [isolated, final]
        static readonly Dictionary<char, ushort[]> LamAlef = new Dictionary<char, ushort[]>
        {
            { 'آ', new ushort[] { 0xFEF5, 0xFEF6 } },
            { 'أ', new ushort[] { 0xFEF7, 0xFEF8 } },
            { 'إ', new ushort[] { 0xFEF9, 0xFEFA } },
            { 'ا', new ushort[] { 0xFEFB, 0xFEFC } },
        };

        const char Tatweel = 'ـ';

        static bool IsArabicLetter(char c) => Forms.ContainsKey(c) || c == Tatweel;
        static bool IsDiacritic(char c) => c >= 'ً' && c <= 'ْ';
        static bool IsArabic(char c) => (c >= '؀' && c <= 'ۿ') || (c >= 'ﹰ' && c <= '﻿');

        static bool JoinsToNext(char c)
        {
            if (c == Tatweel) return true;
            return Forms.TryGetValue(c, out var f) && f[2] != 0;
        }

        static bool JoinsToPrev(char c)
        {
            if (c == Tatweel) return true;
            return Forms.TryGetValue(c, out var f) && f[1] != 0;
        }

        public static string Shape(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            bool any = false;
            foreach (char c in text) if (IsArabic(c)) { any = true; break; }
            if (!any) return text;

            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Reorder(ShapeLetters(lines[i]));
            return string.Join("\n", lines);
        }

        static char PrevLetter(string s, int i)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if (IsDiacritic(s[j])) continue;
                return s[j];
            }
            return '\0';
        }

        static char NextLetter(string s, int i, out int index)
        {
            for (int j = i + 1; j < s.Length; j++)
            {
                if (IsDiacritic(s[j])) continue;
                index = j;
                return s[j];
            }
            index = -1;
            return '\0';
        }

        static string ShapeLetters(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!Forms.TryGetValue(c, out var forms))
                {
                    sb.Append(c);
                    continue;
                }

                char prev = PrevLetter(s, i);
                bool prevJoins = prev != '\0' && JoinsToNext(prev);

                char next = NextLetter(s, i, out int nextIndex);

                if (c == 'ل' && LamAlef.TryGetValue(next, out var lig))
                {
                    sb.Append((char)(prevJoins ? lig[1] : lig[0]));
                    i = nextIndex; // swallow the alef
                    continue;
                }

                bool nextJoins = next != '\0' && JoinsToPrev(next) && forms[2] != 0;

                ushort glyph;
                if (prevJoins && nextJoins) glyph = forms[3];
                else if (prevJoins) glyph = forms[1];
                else if (nextJoins) glyph = forms[2];
                else glyph = forms[0];

                if (glyph == 0) glyph = prevJoins && forms[1] != 0 ? forms[1] : forms[0];
                sb.Append((char)glyph);
            }
            return sb.ToString();
        }

        static bool IsStrongLtr(char c) => char.IsLetterOrDigit(c) && !IsArabic(c);

        /// <summary>Converts a logical-order RTL line to visual (left-to-right) order.</summary>
        static string Reorder(string s)
        {
            // Split into runs; LTR runs (Latin words/numbers, including the
            // punctuation inside them) keep their order, everything else is RTL.
            var runs = new List<(string text, bool ltr)>();
            int i = 0;
            while (i < s.Length)
            {
                if (IsStrongLtr(s[i]))
                {
                    int start = i;
                    int lastStrong = i;
                    while (i < s.Length && !IsArabic(s[i]))
                    {
                        if (IsStrongLtr(s[i])) lastStrong = i;
                        i++;
                    }
                    // trailing neutrals belong to the RTL context
                    runs.Add((s.Substring(start, lastStrong - start + 1), true));
                    i = lastStrong + 1;
                }
                else
                {
                    int start = i;
                    while (i < s.Length && !IsStrongLtr(s[i])) i++;
                    runs.Add((s.Substring(start, i - start), false));
                }
            }

            var sb = new StringBuilder(s.Length);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                var (text, ltr) = runs[r];
                if (ltr) { sb.Append(text); continue; }
                for (int k = text.Length - 1; k >= 0; k--) sb.Append(Mirror(text[k]));
            }
            return sb.ToString();
        }

        static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '<': return '>';
                case '>': return '<';
                default: return c;
            }
        }
    }
}
