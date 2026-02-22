using System.Text;

namespace CrossplaneSharp;

/// <summary>
/// Reconstructs an NGINX configuration text from a list of <see cref="ConfigBlock"/>
/// directives.  Equivalent to the Python crossplane <c>build()</c> function.
/// </summary>
public class NginxBuilder
{
    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an NGINX config string from <paramref name="directives"/>.
    /// </summary>
    public string Build(IEnumerable<ConfigBlock> directives, BuildOptions? options = null)
    {
        options ??= new BuildOptions();
        var sb = new StringBuilder();
        BuildBlocks(directives, sb, options, depth: 0);

        if (options.Newline && sb.Length > 0 && sb[^1] != '\n')
            sb.Append('\n');

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static void BuildBlocks(
        IEnumerable<ConfigBlock> directives,
        StringBuilder sb,
        BuildOptions options,
        int depth)
    {
        string indent = string.Concat(Enumerable.Repeat(options.Indent, depth));

        foreach (ConfigBlock directive in directives)
        {
            // ── Comment ───────────────────────────────────────────────────────
            if (directive.Directive == "#")
            {
                if (options.IncludeComments)
                {
                    sb.Append(indent);
                    sb.Append('#');
                    if (!string.IsNullOrEmpty(directive.Comment))
                    {
                        sb.Append(' ');
                        sb.Append(directive.Comment);
                    }
                    sb.Append('\n');
                }
                continue;
            }

            sb.Append(indent);
            sb.Append(directive.Directive);

            // Args
            if (directive.Args.Count > 0)
            {
                sb.Append(' ');
                sb.Append(string.Join(" ", directive.Args));
            }

            // Block
            if (directive.Block is not null)
            {
                sb.Append(" {\n");
                BuildBlocks(directive.Block, sb, options, depth + 1);
                sb.Append(indent);
                sb.Append("}\n");
            }
            else
            {
                sb.Append(";\n");
            }
        }
    }
}
