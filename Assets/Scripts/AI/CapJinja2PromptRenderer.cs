using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DungeonExporer.AI
{
    /// <summary>
    /// Minimal Jinja2 renderer for <c>cap_personality.jinja2</c> (macros, if/elif/else, set, filters).
    /// </summary>
    internal static class CapJinja2PromptRenderer
    {
        private static readonly Regex CommentRegex = new(@"\{#-?.*?-?#\}", RegexOptions.Singleline);
        private static readonly Regex MacroDefRegex = new(
            @"\{%-?\s*macro\s+(\w+)\s*\(([^)]*)\)\s*-?%\}(.*?)\{%-?\s*endmacro\s*-?%\}",
            RegexOptions.Singleline);
        private static readonly Regex SetRegex = new(
            @"\{%-?\s*set\s+(\w+)\s*=\s*(.+?)\s*-?%\}",
            RegexOptions.Singleline);
        private static readonly Regex MacroCallRegex = new(@"\{\{\s*(\w+)\s*\(([^)]*)\)\s*\}\}");
        private static readonly Regex VarRegex = new(@"\{\{-?\s*(.+?)\s*-?\}\}");

        public static string Render(string template, IReadOnlyDictionary<string, string> context)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (context != null)
            {
                foreach (KeyValuePair<string, string> pair in context)
                    vars[pair.Key] = pair.Value ?? string.Empty;
            }

            string body = CommentRegex.Replace(template, string.Empty);
            var macros = ParseMacros(ref body);
            ApplySetStatements(ref body, vars);
            body = ExpandMacroCalls(body, macros, vars);
            body = ProcessConditionals(body, vars);
            body = InterpolateVariables(body, vars);
            return NormalizeNewlines(body);
        }

        private static Dictionary<string, Macro> ParseMacros(ref string body)
        {
            var macros = new Dictionary<string, Macro>(StringComparer.Ordinal);
            foreach (Match match in MacroDefRegex.Matches(body))
            {
                string[] paramNames = SplitMacroParams(match.Groups[2].Value);
                macros[match.Groups[1].Value] = new Macro(paramNames, match.Groups[3].Value);
            }

            body = MacroDefRegex.Replace(body, string.Empty);
            return macros;
        }

        private static void ApplySetStatements(ref string body, Dictionary<string, string> vars)
        {
            foreach (Match match in SetRegex.Matches(body))
            {
                string name = match.Groups[1].Value.Trim();
                vars[name] = EvaluateExpression(match.Groups[2].Value.Trim(), vars);
            }

            body = SetRegex.Replace(body, string.Empty);
        }

        private static string ExpandMacroCalls(string body, Dictionary<string, Macro> macros, Dictionary<string, string> vars)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                body = MacroCallRegex.Replace(body, m =>
                {
                    string macroName = m.Groups[1].Value;
                    if (!macros.TryGetValue(macroName, out Macro macro))
                        return m.Value;

                    string[] argValues = EvaluateMacroArgs(m.Groups[2].Value, vars);
                    var local = new Dictionary<string, string>(vars, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < macro.ParamNames.Length && i < argValues.Length; i++)
                        local[macro.ParamNames[i]] = argValues[i];

                    string expanded = ExpandMacroCalls(macro.Body, macros, local);
                    expanded = ProcessConditionals(expanded, local);
                    expanded = InterpolateVariables(expanded, local);
                    changed = true;
                    return expanded;
                });

                if (!changed)
                    break;
            }

            return body;
        }

        private static string ProcessConditionals(string body, Dictionary<string, string> vars)
        {
            int guard = 0;
            while (guard++ < 32)
            {
                int ifIndex = body.IndexOf("{% if ", StringComparison.Ordinal);
                if (ifIndex < 0)
                    break;

                int endifIndex = FindMatchingEndif(body, ifIndex);
                if (endifIndex < 0)
                    break;

                int blockEnd = body.IndexOf("%}", endifIndex, StringComparison.Ordinal) + 2;
                string segment = body.Substring(ifIndex, blockEnd - ifIndex);
                string chosen = ChooseIfSegment(segment, vars);
                body = body.Substring(0, ifIndex) + chosen + body.Substring(blockEnd);
            }

            return body;
        }

        private static int FindMatchingEndif(string body, int ifIndex)
        {
            int depth = 0;
            int i = ifIndex;
            while (i < body.Length)
            {
                if (!body.AsSpan(i).StartsWith("{%"))
                {
                    i++;
                    continue;
                }

                int close = body.IndexOf("%}", i, StringComparison.Ordinal);
                if (close < 0)
                    return -1;

                string tag = body.Substring(i + 2, close - i - 2).Trim();
                if (tag.StartsWith("if ", StringComparison.Ordinal))
                    depth++;
                else if (tag == "endif")
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }

                i = close + 2;
            }

            return -1;
        }

        private static string ChooseIfSegment(string segment, Dictionary<string, string> vars)
        {
            var tags = new List<(string kind, string condition, int tagOpen, int contentStart)>();
            int i = 0;
            while (i < segment.Length)
            {
                if (!segment.AsSpan(i).StartsWith("{%"))
                {
                    i++;
                    continue;
                }

                int tagOpen = i;
                int close = segment.IndexOf("%}", i, StringComparison.Ordinal);
                if (close < 0)
                    break;

                string tag = segment.Substring(i + 2, close - i - 2).Trim();
                if (tag.StartsWith("if ", StringComparison.Ordinal))
                    tags.Add(("if", tag.Substring(3).Trim(), tagOpen, close + 2));
                else if (tag.StartsWith("elif ", StringComparison.Ordinal))
                    tags.Add(("elif", tag.Substring(5).Trim(), tagOpen, close + 2));
                else if (tag == "else")
                    tags.Add(("else", null, tagOpen, close + 2));
                else if (tag == "endif")
                    break;

                i = close + 2;
            }

            for (int t = 0; t < tags.Count; t++)
            {
                int bodyStart = tags[t].contentStart;
                int bodyEnd = t + 1 < tags.Count ? tags[t + 1].tagOpen : segment.IndexOf("{% endif", StringComparison.Ordinal);
                if (bodyEnd < 0)
                    bodyEnd = segment.Length;

                string branchBody = segment.Substring(bodyStart, bodyEnd - bodyStart);
                if (tags[t].kind == "else")
                    return branchBody;

                if (EvaluateCondition(tags[t].condition, vars))
                    return branchBody;
            }

            return string.Empty;
        }

        private static bool EvaluateCondition(string condition, Dictionary<string, string> vars)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;

            if (condition.Contains(" and ", StringComparison.Ordinal))
            {
                string[] parts = condition.Split(new[] { " and " }, StringSplitOptions.None);
                for (int p = 0; p < parts.Length; p++)
                {
                    if (!EvaluateCondition(parts[p].Trim(), vars))
                        return false;
                }

                return true;
            }

            if (condition.EndsWith(" is defined", StringComparison.Ordinal))
            {
                string key = condition.Substring(0, condition.Length - " is defined".Length).Trim();
                return vars.ContainsKey(key);
            }

            if (condition.Contains('|', StringComparison.Ordinal))
                return !string.IsNullOrWhiteSpace(EvaluateExpression(condition, vars));

            int neq = condition.IndexOf("!=", StringComparison.Ordinal);
            if (neq > 0)
            {
                string left = EvaluateExpression(condition.Substring(0, neq).Trim(), vars);
                string right = Unquote(condition.Substring(neq + 2).Trim());
                return !string.Equals(left, right, StringComparison.Ordinal);
            }

            int eq = condition.IndexOf("==", StringComparison.Ordinal);
            if (eq > 0)
            {
                string left = EvaluateExpression(condition.Substring(0, eq).Trim(), vars);
                string right = Unquote(condition.Substring(eq + 2).Trim());
                return string.Equals(left, right, StringComparison.Ordinal);
            }

            return !string.IsNullOrWhiteSpace(EvaluateExpression(condition, vars));
        }

        private static string InterpolateVariables(string body, Dictionary<string, string> vars)
        {
            return VarRegex.Replace(body, m => EvaluateExpression(m.Groups[1].Value.Trim(), vars));
        }

        private static string EvaluateExpression(string expr, Dictionary<string, string> vars)
        {
            expr = expr.Trim();
            if (expr.Contains(" if ", StringComparison.Ordinal) && expr.Contains(" else ", StringComparison.Ordinal))
                return EvaluateTernary(expr, vars);

            string[] parts = expr.Split('|');
            string value = ResolveValue(parts[0].Trim(), vars);
            for (int i = 1; i < parts.Length; i++)
                value = ApplyFilter(parts[i].Trim(), value, vars);

            return value ?? string.Empty;
        }

        private static string EvaluateTernary(string expr, Dictionary<string, string> vars)
        {
            int ifIdx = expr.IndexOf(" if ", StringComparison.Ordinal);
            int elseIdx = expr.IndexOf(" else ", StringComparison.Ordinal);
            if (ifIdx < 0 || elseIdx < 0)
                return expr;

            string trueVal = Unquote(expr.Substring(0, ifIdx).Trim());
            string cond = expr.Substring(ifIdx + 5, elseIdx - ifIdx - 5).Trim();
            string falseVal = Unquote(expr.Substring(elseIdx + 6).Trim());
            return EvaluateCondition(cond, vars) ? trueVal : falseVal;
        }

        private static string ApplyFilter(string filter, string value, Dictionary<string, string> vars)
        {
            if (filter.StartsWith("default(", StringComparison.Ordinal) && filter.EndsWith(')'))
            {
                string fallback = Unquote(filter.Substring(8, filter.Length - 9).Trim());
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }

            if (filter == "trim")
                return value?.Trim() ?? string.Empty;

            return value ?? string.Empty;
        }

        private static string ResolveValue(string token, Dictionary<string, string> vars)
        {
            if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
                return Unquote(token);
            if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
                return Unquote(token);

            return vars.TryGetValue(token, out string v) ? v : string.Empty;
        }

        private static string[] EvaluateMacroArgs(string argList, Dictionary<string, string> vars)
        {
            if (string.IsNullOrWhiteSpace(argList))
                return Array.Empty<string>();

            string[] raw = argList.Split(',');
            var values = new string[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                values[i] = EvaluateExpression(raw[i].Trim(), vars);
            return values;
        }

        private static string[] SplitMacroParams(string paramList)
        {
            if (string.IsNullOrWhiteSpace(paramList))
                return Array.Empty<string>();

            string[] raw = paramList.Split(',');
            for (int i = 0; i < raw.Length; i++)
                raw[i] = raw[i].Trim();
            return raw;
        }

        private static string Unquote(string text)
        {
            text = text.Trim();
            if (text.Length >= 2 &&
                ((text[0] == '\'' && text[^1] == '\'') || (text[0] == '"' && text[^1] == '"')))
                return text.Substring(1, text.Length - 2);
            return text;
        }

        private static string NormalizeNewlines(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.TrimEnd();
                if (trimmed.Length == 0)
                {
                    if (sb.Length > 0 && sb[^1] != '\n')
                        sb.Append('\n');
                    continue;
                }

                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(trimmed);
            }

            return sb.ToString().Trim() + "\n";
        }

        private sealed class Macro
        {
            public Macro(string[] paramNames, string body)
            {
                ParamNames = paramNames;
                Body = body;
            }

            public string[] ParamNames { get; }
            public string Body { get; }
        }
    }
}
