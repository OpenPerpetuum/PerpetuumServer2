using System;
using System.Linq;

namespace Perpetuum.Services.Sparky
{
    public static class SparkyQuestionCandidateFilter
    {
        private static readonly char[] QuestionMarks = { '?', '？' };

        private static readonly string[] Greetings =
        {
            "guten tag", "hello", "hallo", "здравствуйте", "привет", "olá", "ola", "hey", "hi", "oi"
        };

        private static readonly string[] QuestionOpenings =
        {
            // English
            "does anyone know", "anybody know", "anyone know", "can someone tell",
            "who", "what", "when", "where", "why", "how", "which", "is", "are", "was", "were",
            "do", "does", "did", "can", "could", "would", "should", "has", "have",

            // German
            "kann mir jemand sagen", "weiß jemand", "weiss jemand", "kennt jemand",
            "wer", "was", "wann", "wo", "warum", "wieso", "wie", "welche", "ist", "sind", "war",
            "hat", "haben", "kann", "können",

            // Portuguese
            "alguém poderia", "alguem poderia", "alguém sabe", "alguem sabe",
            "o que", "por que", "porque", "quem", "qual", "quais", "quando", "onde", "como",
            "é", "está", "são", "tem", "têm", "pode", "podem",

            // Russian
            "кто-нибудь знает", "кто нибудь знает", "кто-то знает", "есть ли", "можно ли", "подскажите",
            "кто", "что", "когда", "где", "почему", "зачем", "как", "какой", "какая"
        };

        public static bool IsCandidate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.IndexOfAny(QuestionMarks) >= 0)
            {
                return true;
            }

            string candidate = RemoveLeadingGreetingsAndPunctuation(text);
            return QuestionOpenings.Any(opening => StartsWithCompletePhrase(candidate, opening));
        }

        private static string RemoveLeadingGreetingsAndPunctuation(string text)
        {
            string candidate = text;

            while (true)
            {
                candidate = TrimLeadingPunctuation(candidate);
                string greeting = Greetings.FirstOrDefault(value => StartsWithCompletePhrase(candidate, value));
                if (greeting == null)
                {
                    return candidate;
                }

                candidate = candidate.Substring(greeting.Length);
            }
        }

        private static string TrimLeadingPunctuation(string text)
        {
            int start = 0;
            while (start < text.Length && (char.IsWhiteSpace(text[start]) || char.IsPunctuation(text[start])))
            {
                start++;
            }

            return start == 0 ? text : text.Substring(start);
        }

        private static bool StartsWithCompletePhrase(string text, string phrase)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                !text.StartsWith(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return text.Length == phrase.Length ||
                   char.IsWhiteSpace(text[phrase.Length]) ||
                   char.IsPunctuation(text[phrase.Length]);
        }
    }
}
