using System.Text;

namespace CrossplaneSharp;

/// <summary>
/// Reconstructs an NGINX configuration string from a list of <see cref="ConfigBlock"/> directives.
/// C# port of Python crossplane <c>builder.py</c> — including <c>_enquote</c>,
/// <c>if(…)</c> syntax, same-line inline comments, optional header, and <c>BuildFiles</c>.
/// </summary>
public class NginxBuilder
{
    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an NGINX config string from <paramref name="block"/>.
    /// Equivalent to Python <c>crossplane.build(payload, …)</c>.
    /// </summary>
    public string Build(IEnumerable<ConfigBlock> block, BuildOptions? options = null)
    {
        options ??= new BuildOptions();
        string padding = options.Tabs ? "\t" : new string(' ', options.Indent);

        var head = new StringBuilder();
        if (options.Header)
        {
            head.AppendLine("# This config was built from JSON using NGINX crossplane.");
            head.AppendLine("# If you encounter any bugs please report them here:");
            head.AppendLine("# https://github.com/nginxinc/crossplane/issues");
            head.AppendLine();
        }

        string body = BuildBlock("", block.ToList(), padding, depth: 0, lastLine: 0);
        return head + body;
    }

    /// <summary>
    /// Writes each config entry from <paramref name="payload"/> to disk.
    /// Equivalent to Python <c>crossplane.build_files(payload, …)</c>.
    /// </summary>
    public void BuildFiles(ParseResult payload, string? dirname = null, BuildOptions? options = null)
    {
        dirname ??= Directory.GetCurrentDirectory();
        options ??= new BuildOptions();

        foreach (var config in payload.Config)
        {
            string path = config.File;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(dirname, path);

            string? dirPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            string output = Build(config.Parsed, options).TrimEnd() + "\n";
            File.WriteAllText(path, output, Encoding.UTF8);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Core block builder  (mirrors Python _build_block)
    // ──────────────────────────────────────────────────────────────────────────

    private static string BuildBlock(
        string output, List<ConfigBlock> block,
        string padding, int depth, int lastLine)
    {
        string margin = string.Concat(Enumerable.Repeat(padding, depth));

        foreach (var stmt in block)
        {
            string directive = Enquote(stmt.Directive);
            int line = stmt.Line;

            // ── inline comment on same line as previous directive ─────────────
            if (directive == "#" && lastLine != 0 && line == lastLine)
            {
                var inlineText = stmt.Comment ?? "";
                output += inlineText.Length > 0 ? " # " + inlineText : " #";
                continue;
            }

            string built;
            if (directive == "#")
            {
                var commentText = stmt.Comment ?? "";
                built = commentText.Length > 0 ? "# " + commentText : "#";
            }
            else
            {
                var args = stmt.Args.Select(Enquote).ToList();

                if (directive == "if")
                    built = "if (" + string.Join(" ", args) + ")";
                else if (args.Count > 0)
                    built = directive + " " + string.Join(" ", args);
                else
                    built = directive;

                if (stmt.Block is null)
                {
                    built += ";";
                }
                else
                {
                    built += " {";
                    built = BuildBlock(built, stmt.Block, padding, depth + 1, line);
                    built += "\n" + margin + "}";
                }
            }

            output += (output.Length > 0 ? "\n" : "") + margin + built;
            lastLine = line;
        }

        return output;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // _enquote  (mirrors Python builder._enquote / _needs_quotes)
    // ──────────────────────────────────────────────────────────────────────────

    private static string Enquote(string arg)
    {
        if (!NeedsQuotes(arg)) return arg;
        // repr-like: use JSON-style escaping (mirrors Python repr().replace('\\\\','\\'))
        string r = System.Text.Json.JsonSerializer.Serialize(arg);
        r = r.Replace("\\\\", "\\");
        return r;
    }

    private static bool NeedsQuotes(string s)
    {
        if (s.Length == 0) return true;

        using var chars = Escape(s).GetEnumerator();
        if (!chars.MoveNext()) return true;

        string first = chars.Current;
        if (first.Length == 1 && char.IsWhiteSpace(first[0])) return true;
        if (first == "{" || first == "}" || first == ";" || first == "\"" || first == "'" || first == "${")
            return true;

        bool expanding = false;
        string last = first;
        while (chars.MoveNext())
        {
            last = chars.Current;
            if (last.Length == 1 && (char.IsWhiteSpace(last[0]) || last == "{" || last == ";" || last == "\"" || last == "'"))
                return true;
            if (last == (expanding ? "${" : "}")) return true;
            if (last == (expanding ? "}" : "${")) expanding = !expanding;
        }
        return last == "\\" || last == "$" || expanding;
    }

    /// <summary>Mirrors Python builder._escape generator.</summary>
    private static IEnumerable<string> Escape(string s)
    {
        string prev = "", cur = "";
        foreach (char c in s)
        {
            cur = c.ToString();
            if (prev == "\\" || prev + cur == "${")
            {
                prev += cur;
                yield return prev;
                prev = ""; cur = "";
                continue;
            }
            if (prev == "$") yield return prev;
            if (cur != "\\" && cur != "$") yield return cur;
            prev = cur;
        }
        if (cur == "\\" || cur == "$") yield return cur;
    }
}
