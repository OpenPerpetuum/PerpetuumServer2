namespace Perpetuum.AdminTool.Translations
{
    public record LanguageInfo(int Id, string Name);

    public static class LanguageCatalog
    {
        public static readonly IReadOnlyList<LanguageInfo> All = new[]
        {
            new LanguageInfo(0,  "English"),
            new LanguageInfo(1,  "Hungarian"),
            new LanguageInfo(2,  "German"),
            new LanguageInfo(3,  "Portuguese"),
            new LanguageInfo(4,  "Russian"),
            new LanguageInfo(5,  "French"),
            new LanguageInfo(6,  "Spanish"),
            new LanguageInfo(7,  "Polish"),
            new LanguageInfo(8,  "Slovenian"),
            new LanguageInfo(9,  "Romanian"),
            new LanguageInfo(10, "Norwegian"),
            new LanguageInfo(11, "Greek"),
            new LanguageInfo(12, "Finnish"),
            new LanguageInfo(13, "Italian"),
            new LanguageInfo(14, "Turkish"),
            new LanguageInfo(15, "Estonian"),
            new LanguageInfo(16, "Swedish"),
            new LanguageInfo(17, "Dutch"),
        };

        public static string NameOf(int id) =>
            All.FirstOrDefault(language => language.Id == id)?.Name ?? $"Lang {id}";
    }
}
