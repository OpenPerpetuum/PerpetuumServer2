using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Perpetuum.AdminTool.Editing
{
    public static class SqlScriptBuilder
    {
        public static string Build(IEnumerable<IPendingChange> changes, string? authorEmail = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- Perpetuum.AdminTool generated script");
            sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            if (!string.IsNullOrWhiteSpace(authorEmail))
            {
                sb.AppendLine($"-- Author: {authorEmail}");
            }
            sb.AppendLine();
            sb.AppendLine("SET XACT_ABORT ON;");
            sb.AppendLine("BEGIN TRANSACTION;");
            sb.AppendLine();

            int i = 1;
            foreach (var change in changes)
            {
                sb.AppendLine($"-- [{i}] {change.Description}");
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

        public static string BuildFileName(string prefix, string? name = null)
        {
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (string.IsNullOrWhiteSpace(name))
                return $"{prefix}_{ts}.sql";
            var safe = Regex.Replace(
                Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9_]", "_"),
                @"_+", "_").Trim('_');
            return $"{prefix}_{safe}_{ts}.sql";
        }
    }
}
