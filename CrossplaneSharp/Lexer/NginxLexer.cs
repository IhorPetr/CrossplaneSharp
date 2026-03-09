using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrossplaneSharp.Exceptions;

namespace CrossplaneSharp
{

    /// <summary>
    /// Tokenises an NGINX configuration file into a stream of <see cref="NgxToken"/> values.
    /// </summary>
    public class NginxLexer
    {
        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Tokenises the file at <paramref name="filename"/> and returns tokens as a list.
        /// Raises <see cref="NgxParserSyntaxError"/> on unbalanced braces.
        /// </summary>
        public IReadOnlyList<NgxToken> Lex(string filename)
        {
            string content = File.ReadAllText(filename, Encoding.UTF8);
            return LexContent(content, filename).ToList();
        }

        /// <summary>
        /// Tokenises an in-memory config string. Used by tests and the parser.
        /// </summary>
        public IReadOnlyList<NgxToken> LexString(string content, string filename = null)
            => LexContent(content, filename).ToList();

        /// <summary>
        /// Tokenises the file at <paramref name="filename"/> and returns a lazy sequence.
        /// Used internally by <see cref="NginxParser"/> and exposed publicly for streaming use.
        /// </summary>
        public IEnumerable<NgxToken> Tokenize(string filename)
        {
            string content = File.ReadAllText(filename, Encoding.UTF8);
            return LexContent(content, filename);
        }

        /// <summary>
        /// Alias for <see cref="LexString"/> — tokenises an in-memory config string.
        /// </summary>
        public IEnumerable<NgxToken> TokenizeContent(string content, string filename = null)
            => LexContent(content, filename);

        // -------------------------------------------------------------------------
        // Core implementation
        // -------------------------------------------------------------------------

        private static IEnumerable<NgxToken> LexContent(string text, string filename)
        {
            var raw = LexFileObject(text);
            return BalanceBraces(raw, filename);
        }

        /// <summary>
        /// Main tokeniser. Handles:
        ///   • whitespace (skip / flush buffer)
        ///   • comments  (#…\n)
        ///   • quoted strings (single or double, with escape handling)
        ///   • variable expansion (${ … })
        ///   • delimiters  { } ;
        ///   • backslash-escaped characters
        /// </summary>
        private static IEnumerable<NgxToken> LexFileObject(string text)
        {
            // Build a list of (singleCharOrEscapePair, lineNumber).
            var chars = BuildCharStream(text);
            int pos = 0;

            string tokenBuf = "";
            int tokenLine = 0;

            while (pos < chars.Count)
            {
                var (ch, line) = chars[pos];

                // ── whitespace ────────────────────────────────────────────────────
                if (IsSpace(ch))
                {
                    if (tokenBuf.Length > 0)
                    {
                        yield return new NgxToken(tokenBuf, tokenLine, false);
                        tokenBuf = "";
                    }
                    // skip all whitespace
                    while (pos < chars.Count && IsSpace(chars[pos].Ch))
                        pos++;
                    continue;
                }

                // ── comment ───────────────────────────────────────────────────────
                if (tokenBuf.Length == 0 && ch == "#")
                {
                    var sb = new StringBuilder();
                    while (pos < chars.Count && !chars[pos].Ch.EndsWith("\n"))
                    {
                        sb.Append(chars[pos].Ch);
                        pos++;
                    }
                    yield return new NgxToken(sb.ToString(), line, false);
                    continue;
                }

                // ── record start line for token ───────────────────────────────────
                if (tokenBuf.Length == 0)
                    tokenLine = line;

                // ── variable expansion: ${var} ────────────────────────────────────
                // When the last char of the buffer is $ and next char is {,
                // keep reading until we close with }
                if (tokenBuf.Length > 0 && tokenBuf[tokenBuf.Length - 1] == '$' && ch == "{")
                {
                    while (pos < chars.Count && tokenBuf[tokenBuf.Length - 1] != '}' && !IsSpace(chars[pos].Ch))
                    {
                        tokenBuf += chars[pos].Ch;
                        pos++;
                    }
                    continue;
                }

                // ── quoted string ─────────────────────────────────────────────────
                if (ch == "\"" || ch == "'")
                {
                    // A quote inside an existing token is treated as a plain char
                    if (tokenBuf.Length > 0)
                    {
                        tokenBuf += ch;
                        pos++;
                        continue;
                    }

                    string quote = ch;
                    pos++; // skip opening quote

                    var sb = new StringBuilder();
                    while (pos < chars.Count)
                    {
                        var (qch, qline) = chars[pos];
                        if (qch == quote)
                        {
                            pos++; // skip closing quote
                            break;
                        }
                        // escaped quote: \' or \"  appears as 2-char escape-pair
                        sb.Append(qch == "\\" + quote ? quote : qch);
                        pos++;
                    }

                    yield return new NgxToken(sb.ToString(), tokenLine, true);
                    tokenBuf = "";
                    continue;
                }

                // ── delimiters ────────────────────────────────────────────────────
                if (ch == "{" || ch == "}" || ch == ";")
                {
                    if (tokenBuf.Length > 0)
                    {
                        yield return new NgxToken(tokenBuf, tokenLine, false);
                        tokenBuf = "";
                    }
                    yield return new NgxToken(ch, line, false);
                    pos++;
                    continue;
                }

                // ── regular character ─────────────────────────────────────────────
                tokenBuf += ch;
                pos++;
            }

            // flush any remaining buffer
            if (tokenBuf.Length > 0)
                yield return new NgxToken(tokenBuf, tokenLine, false);
        }

        // -------------------------------------------------------------------------

        private struct CharEntry
        {
            public string Ch;
            public int Line;
            public CharEntry(string ch, int line) { Ch = ch; Line = line; }
            public void Deconstruct(out string ch, out int line) { ch = Ch; line = Line; }
        }

        /// <summary>
        /// Mirrors Python <c>_iterescape</c> + <c>_iterlinecount</c>:
        /// backslash + next-char become one 2-char entry; newlines increment line.
        /// </summary>
        private static IReadOnlyList<CharEntry> BuildCharStream(string text)
        {
            var list = new List<CharEntry>(text.Length);
            int line = 1;
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    string pair = "\\" + next;
                    if (next == '\n') line++;
                    list.Add(new CharEntry(pair, line));
                    i += 2;
                }
                else
                {
                    string s = c.ToString();
                    list.Add(new CharEntry(s, line));
                    if (c == '\n') line++;
                    i++;
                }
            }
            return list;
        }

        /// <summary>
        /// Mirrors Python <c>_balance_braces</c>: raises on unbalanced { }.
        /// </summary>
        private static IEnumerable<NgxToken> BalanceBraces(IEnumerable<NgxToken> tokens, string filename)
        {
            int depth = 0;
            NgxToken last = null;

            foreach (var t in tokens)
            {
                last = t;
                if (!t.IsQuoted)
                {
                    if (t.Value == "}") depth--;
                    else if (t.Value == "{") depth++;

                    if (depth < 0)
                        throw new NgxParserSyntaxError("unexpected \"}\"", filename, t.Line);
                }
                yield return t;
            }

            if (depth > 0)
                throw new NgxParserSyntaxError(
                    "unexpected end of file, expecting \"}\"", filename, last?.Line);
        }

        private static bool IsSpace(string s) => s.Length == 1 && char.IsWhiteSpace(s[0]);
    }
}
