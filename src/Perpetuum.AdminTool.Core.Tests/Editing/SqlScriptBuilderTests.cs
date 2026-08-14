using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Core.Tests.Editing;

public sealed class SqlScriptBuilderTests
{
    [Fact]
    public void Build_WrapsChangesInOneAuditableTransaction()
    {
        DateTimeOffset generatedAt = new(2026, 8, 14, 12, 34, 56, TimeSpan.Zero);
        IPendingChange[] changes =
        [
            new RawSqlChange("update first row", "UPDATE first_table SET value=1"),
            new RawSqlChange("delete second row", "DELETE FROM second_table WHERE id=2;", true)
        ];

        string script = SqlScriptBuilder.Build(changes, "admin@example.invalid", generatedAt);

        Assert.Contains("-- Generated: 2026-08-14 12:34:56 UTC", script);
        Assert.Contains("-- Author: admin@example.invalid", script);
        Assert.Contains("SET XACT_ABORT ON;", script);
        Assert.Contains("BEGIN TRANSACTION;", script);
        Assert.Contains("-- [1] update first row", script);
        Assert.Contains("UPDATE first_table SET value=1\n;", NormalizeLineEndings(script));
        Assert.Contains("-- [2] delete second row", script);
        Assert.EndsWith("COMMIT TRANSACTION;\n", NormalizeLineEndings(script));
    }

    [Fact]
    public void Build_SanitizesUntrustedCommentText()
    {
        string script = SqlScriptBuilder.Build(
            [new RawSqlChange("safe\r\nDROP TABLE accounts", "SELECT 1;")],
            "admin@example.invalid\nDELETE FROM accounts;");

        Assert.Contains("-- Author: admin@example.invalid DELETE FROM accounts;", script);
        Assert.Contains("-- [1] safe DROP TABLE accounts", script);
        Assert.DoesNotContain("\nDROP TABLE accounts", NormalizeLineEndings(script));
        Assert.DoesNotContain("\nDELETE FROM accounts", NormalizeLineEndings(script));
    }

    [Theory]
    [InlineData("Season Export", "New Player / Test", "season_export_new_player_test_20260814_123456.sql")]
    [InlineData("***", "???", "changes_unnamed_20260814_123456.sql")]
    public void BuildFileName_UsesPortableSanitizedSegments(
        string prefix,
        string name,
        string expected)
    {
        DateTimeOffset generatedAt = new(2026, 8, 14, 12, 34, 56, TimeSpan.Zero);

        string actual = SqlScriptBuilder.BuildFileName(prefix, name, generatedAt);

        Assert.Equal(expected, actual);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n");
    }
}
