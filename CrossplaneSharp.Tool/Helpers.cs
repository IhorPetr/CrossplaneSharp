using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrossplaneSharp.Tool
{
    internal static class Helpers
    {
        internal static void WriteOutput(string content, string? outFile)
        {
            if (outFile == null) Console.Write(content);
            else File.WriteAllText(outFile, content, Encoding.UTF8);
        }

        internal static string ResolvePath(string path, string baseDir) =>
            Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);

        internal static string SerializeJson(object obj, int indent)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented          = indent >= 0,
                PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            string raw = JsonSerializer.Serialize(obj, jsonOptions);
            if (indent > 0 && indent != 2) raw = ReIndent(raw, indent);
            return raw;
        }

        internal static ParseResult DeserializeParseResult(string json)
        {
            var result = JsonSerializer.Deserialize<ParseResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize JSON payload.");
            return result;
        }

        internal static string Enquote(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            if (arg.Any(c => char.IsWhiteSpace(c) || c == '{' || c == '}' || c == ';' || c == '"' || c == '\''))
                return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            return arg;
        }

        private static string ReIndent(string json, int spaces)
        {
            string unit = new string(' ', spaces);
            var sb = new StringBuilder();
            int depth = 0;
            bool inString = false, escape = false;

            foreach (char ch in json)
            {
                if (escape) { sb.Append(ch); escape = false; continue; }
                if (ch == '\\' && inString) { sb.Append(ch); escape = true; continue; }
                if (ch == '"') { inString = !inString; sb.Append(ch); continue; }
                if (inString) { sb.Append(ch); continue; }

                switch (ch)
                {
                    case '{': case '[':
                        sb.Append(ch); sb.Append('\n'); depth++;
                        sb.Append(string.Concat(Enumerable.Repeat(unit, depth))); break;
                    case '}': case ']':
                        sb.Append('\n'); depth--;
                        sb.Append(string.Concat(Enumerable.Repeat(unit, depth)));
                        sb.Append(ch); break;
                    case ',':
                        sb.Append(ch); sb.Append('\n');
                        sb.Append(string.Concat(Enumerable.Repeat(unit, depth))); break;
                    case ':':
                        sb.Append(ch); sb.Append(' '); break;
                    case ' ': case '\n': case '\r': case '\t':
                        break;
                    default:
                        sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }
    }
}

