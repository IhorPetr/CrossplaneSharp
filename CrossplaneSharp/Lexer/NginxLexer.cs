using System.Text;

namespace CrossplaneSharp;

/// <summary>
/// Tokenises an NGINX configuration file into a stream of <see cref="NgxToken"/> values.
/// Equivalent to the Python crossplane <c>lex()</c> function.
/// </summary>
public class NginxLexer
{
    /// <summary>
    /// Tokenises the file at <paramref name="filename"/> and yields tokens one by one.
    /// </summary>
    public IEnumerable<NgxToken> Tokenize(string filename)
    {
        string content = File.ReadAllText(filename);
        return TokenizeContent(content);
    }

    /// <summary>
    /// Tokenises an NGINX config from a string and yields tokens one by one.
    /// Useful for testing without touching the file system.
    /// </summary>
    public IEnumerable<NgxToken> TokenizeContent(string content)
    {
        int line = 1;
        int i = 0;
        int length = content.Length;

        while (i < length)
        {
            char c = content[i];

            // ── Newline ──────────────────────────────────────────────────────────
            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }

            // ── Other whitespace ─────────────────────────────────────────────────
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // ── Comment ──────────────────────────────────────────────────────────
            if (c == '#')
            {
                int tokenLine = line;
                int start = i;
                while (i < length && content[i] != '\n')
                    i++;
                yield return new NgxToken(content[start..i], tokenLine, false);
                continue;
            }

            // ── Special single-character tokens ──────────────────────────────────
            if (c is '{' or '}' or ';')
            {
                yield return new NgxToken(c.ToString(), line, false);
                i++;
                continue;
            }

            // ── Quoted string ────────────────────────────────────────────────────
            if (c is '"' or '\'')
            {
                char quote = c;
                int tokenLine = line;
                var sb = new StringBuilder();
                i++; // skip opening quote

                while (i < length)
                {
                    char ch = content[i];
                    if (ch == '\n') line++;

                    if (ch == '\\' && i + 1 < length)
                    {
                        // preserve escape sequence literally
                        sb.Append(ch);
                        char escaped = content[i + 1];
                        if (escaped == '\n') line++;
                        sb.Append(escaped);
                        i += 2;
                        continue;
                    }

                    if (ch == quote)
                    {
                        i++; // skip closing quote
                        break;
                    }

                    sb.Append(ch);
                    i++;
                }

                yield return new NgxToken(sb.ToString(), tokenLine, true);
                continue;
            }

            // ── Regular (unquoted) token ─────────────────────────────────────────
            {
                int tokenLine = line;
                var sb = new StringBuilder();

                while (i < length)
                {
                    char ch = content[i];

                    if (char.IsWhiteSpace(ch) || ch is '{' or '}' or ';' or '"' or '\'')
                        break;

                    if (ch == '\\' && i + 1 < length)
                    {
                        sb.Append(ch);
                        sb.Append(content[i + 1]);
                        i += 2;
                        continue;
                    }

                    sb.Append(ch);
                    i++;
                }

                if (sb.Length > 0)
                    yield return new NgxToken(sb.ToString(), tokenLine, false);
            }
        }
    }
}
