using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ASB.ExcelImport.Editor
{
    internal static class ExcelListParser
    {
        public static List<string> Split(string raw, bool isStringList, ListDelimiter stringDelimiter)
        {
            string value = NormalizeListShell(raw);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            if (isStringList)
            {
                return stringDelimiter == ListDelimiter.Backslash
                    ? SplitBackslash(value)
                    : SplitComma(value);
            }

            return ContainsBackslashTokenBoundary(value)
                ? SplitBackslash(value)
                : SplitComma(value);
        }

        public static bool CanParseList(string raw, ExcelSchemaFieldType fieldType, ListDelimiter delimiter)
        {
            bool isStringList = fieldType == ExcelSchemaFieldType.ListString;
            List<string> tokens = Split(raw, isStringList, delimiter);

            switch (fieldType)
            {
                case ExcelSchemaFieldType.ListInt:
                    return tokens.All(CanParseInt);
                case ExcelSchemaFieldType.ListFloat:
                    return tokens.All(CanParseFloat);
                case ExcelSchemaFieldType.ListBool:
                    return tokens.All(CanParseBool);
                case ExcelSchemaFieldType.ListString:
                    return true;
                default:
                    return true;
            }
        }

        public static string FormatBackslashStringList(IList list)
        {
            if (list == null || list.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                parts[i] = EscapeBackslashToken(list[i]?.ToString() ?? string.Empty);
            }

            return string.Join("\\", parts);
        }

        private static string NormalizeListShell(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string value = raw.Trim().Trim('"').Trim();
            if (value.Length >= 2 && value[0] == '{' && value[value.Length - 1] == '}')
            {
                value = value.Substring(1, value.Length - 2).Trim();
            }

            return value;
        }

        private static List<string> SplitComma(string value)
        {
            return value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        private static List<string> SplitBackslash(string value)
        {
            var result = new List<string>();
            var token = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current != '\\')
                {
                    token.Append(current);
                    continue;
                }

                if (i + 1 < value.Length)
                {
                    char next = value[i + 1];
                    if (next == 'n')
                    {
                        token.Append('\n');
                        i++;
                        continue;
                    }

                    if (next == 't')
                    {
                        token.Append('\t');
                        i++;
                        continue;
                    }

                    if (next == '\\')
                    {
                        token.Append('\\');
                        i++;
                        continue;
                    }
                }

                AddToken(result, token);
            }

            AddToken(result, token);
            return result;
        }

        private static void AddToken(List<string> result, StringBuilder token)
        {
            string value = token.ToString().Trim();
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }

            token.Length = 0;
        }

        private static bool ContainsBackslashTokenBoundary(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\')
                {
                    continue;
                }

                if (i + 1 >= value.Length)
                {
                    return true;
                }

                char next = value[i + 1];
                if (next != 'n' && next != 't' && next != '\\')
                {
                    return true;
                }

                i++;
            }

            return false;
        }

        private static string EscapeBackslashToken(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static bool CanParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                   float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static bool CanParseFloat(string value)
        {
            return float.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static bool CanParseBool(string value)
        {
            return bool.TryParse(value, out _) ||
                   value == "0" ||
                   value == "1" ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("n", StringComparison.OrdinalIgnoreCase);
        }
    }
}
