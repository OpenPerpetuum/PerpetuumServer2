using System.Text.RegularExpressions;

namespace Perpetuum.Services.Sparky
{
    internal static class SparkyQuestionParser
    {
        private const RegexOptions AddressOptions =
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;

        private static readonly Regex LeadingAddress = new Regex(
            @"^\s*(?:(?:hey|hi|hello)\s+)?sparky\s*,\s*(?<question>.*)$",
            AddressOptions);

        private static readonly Regex TrailingVocative = new Regex(
            @"^(?<question>.*?),\s*sparky\s*(?<punctuation>[?!\.])?\s*$",
            AddressOptions);

        public static bool TryExtractGameQuestion(string message, out string question)
        {
            question = string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            Match leadingAddress = LeadingAddress.Match(message);
            if (leadingAddress.Success)
            {
                question = leadingAddress.Groups["question"].Value.Trim();
                return true;
            }

            Match trailingVocative = TrailingVocative.Match(message);
            if (!trailingVocative.Success)
            {
                return false;
            }

            question = trailingVocative.Groups["question"].Value.Trim();
            string punctuation = trailingVocative.Groups["punctuation"].Value;
            if (question.Length > 0 && punctuation.Length > 0 && !EndsWithSentencePunctuation(question))
            {
                question += punctuation;
            }

            return true;
        }

        private static bool EndsWithSentencePunctuation(string value)
        {
            char lastCharacter = value[^1];
            return lastCharacter is '.' or '!' or '?';
        }
    }
}
