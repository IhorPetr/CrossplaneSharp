using System.Collections.Generic;

namespace CrossplaneSharp
{

    /// <summary>
    /// Top-level entry point for the CrossplaneSharp library,
    /// exposing <c>Lex</c>, <c>Parse</c>, <c>Build</c> and <c>BuildFiles</c>.
    /// </summary>
    public static class Crossplane
    {
        // ──────────────────────────────────────────────────────────────────────────
        // lex()
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tokenises the NGINX config at <paramref name="filename"/>.
        /// Returns a list of <see cref="NgxToken"/> (value, line, isQuoted).
        /// </summary>
        public static IReadOnlyList<NgxToken> Lex(string filename) =>
            new NginxLexer().Lex(filename);

        /// <summary>Tokenises an in-memory config string.</summary>
        public static IReadOnlyList<NgxToken> LexString(string content, string filename = null) =>
            new NginxLexer().LexString(content, filename);

        // ──────────────────────────────────────────────────────────────────────────
        // parse()
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses the NGINX config at <paramref name="filename"/> into a structured
        /// <see cref="ParseResult"/>.
        /// </summary>
        public static ParseResult Parse(string filename, ParseOptions options = null) =>
            new NginxParser().Parse(filename, options);

        // ──────────────────────────────────────────────────────────────────────────
        // build()
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reconstructs an NGINX config string from <paramref name="block"/>.
        /// </summary>
        public static string Build(IEnumerable<ConfigBlock> block, BuildOptions options = null) =>
            new NginxBuilder().Build(block, options);

        // ──────────────────────────────────────────────────────────────────────────
        // build_files()
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes each config entry from <paramref name="payload"/> to disk.
        /// </summary>
        public static void BuildFiles(ParseResult payload, string dirname = null, BuildOptions options = null) =>
            new NginxBuilder().BuildFiles(payload, dirname, options);
    }
}