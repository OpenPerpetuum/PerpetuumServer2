using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Perpetuum.AdminTool.Editing
{
    public static class SqlScriptBuilder
    {
        public static string Build(
            IEnumerable<IPendingChange> changes,
            string? authorEmail = null,
            DateTimeOffset? generatedAt = null)
        {
            ArgumentNullException.ThrowIfNull(changes);

            DateTimeOffset timestamp = (generatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var sb = new StringBuilder();
            sb.AppendLine("-- Perpetuum.AdminTool generated script");
            sb.AppendLine($"-- Generated: {timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            if (!string.IsNullOrWhiteSpace(authorEmail))
            {
                sb.AppendLine($"-- Author: {SanitizeComment(authorEmail)}");
            }
            sb.AppendLine();
            sb.AppendLine("SET XACT_ABORT ON;");
            sb.AppendLine("BEGIN TRANSACTION;");
            sb.AppendLine();

            int i = 1;
            foreach (var change in changes)
            {
                sb.AppendLine($"-- [{i}] {SanitizeComment(change.Description)}");
                var body = change.ToSql().TrimEnd();
                sb.AppendLine(body);
                if (!body.EndsWith(";", StringComparison.Ordinal))
                {
                    sb.AppendLine(";");
                }
                sb.AppendLine();
                i++;
            }

            sb.AppendLine("COMMIT TRANSACTION;");
            return sb.ToString();
        }

        public static string BuildFileName(
            string prefix,
            string? name = null,
            DateTimeOffset? generatedAt = null)
        {
            string safePrefix = SanitizeFileSegment(prefix, "changes");
            string timestamp = (generatedAt ?? DateTimeOffset.UtcNow)
                .ToUniversalTime()
                .ToString("yyyyMMdd_HHmmss");
            if (string.IsNullOrWhiteSpace(name))
            {
                return $"{safePrefix}_{timestamp}.sql";
            }

            string safeName = SanitizeFileSegment(name, "unnamed");
            return $"{safePrefix}_{safeName}_{timestamp}.sql";
        }

        private static string SanitizeComment(string value)
        {
            return Regex.Replace(value, @"[\r\n]+", " ").Trim();
        }

        private static string SanitizeFileSegment(string value, string fallback)
        {
            string safe = Regex.Replace(
                Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9_]", "_"),
                @"_+", "_").Trim('_');
            return string.IsNullOrEmpty(safe) ? fallback : safe;
        }
    }
}
