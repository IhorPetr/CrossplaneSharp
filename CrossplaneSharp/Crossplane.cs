namespace CrossplaneSharp;

/// <summary>
/// Top-level entry point for the CrossplaneSharp library.
/// Mirrors the three core functions of the Python crossplane package:
/// <c>crossplane.lex()</c>, <c>crossplane.parse()</c>, <c>crossplane.build()</c>
/// and <c>crossplane.build_files()</c>.
/// </summary>
public static class Crossplane
{
    // ──────────────────────────────────────────────────────────────────────────
    // lex()
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tokenises the NGINX config at <paramref name="filename"/>.
    /// Equivalent to Python <c>crossplane.lex(filename)</c>.
    /// Returns a list of <see cref="NgxToken"/> (value, line, isQuoted).
    /// </summary>
    public static IReadOnlyList<NgxToken> Lex(string filename) =>
        new NginxLexer().Lex(filename);

    /// <summary>Tokenises an in-memory config string.</summary>
    public static IReadOnlyList<NgxToken> LexString(string content, string? filename = null) =>
        new NginxLexer().LexString(content, filename);

    // ──────────────────────────────────────────────────────────────────────────
    // parse()
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the NGINX config at <paramref name="filename"/> into a structured
    /// <see cref="ParseResult"/>.
    /// Equivalent to Python <c>crossplane.parse(filename, …)</c>.
    /// </summary>
    public static ParseResult Parse(string filename, ParseOptions? options = null) =>
        new NginxParser().Parse(filename, options);

    // ──────────────────────────────────────────────────────────────────────────
    // build()
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reconstructs an NGINX config string from <paramref name="block"/>.
    /// Equivalent to Python <c>crossplane.build(payload, …)</c>.
    /// </summary>
    public static string Build(IEnumerable<ConfigBlock> block, BuildOptions? options = null) =>
        new NginxBuilder().Build(block, options);

    // ──────────────────────────────────────────────────────────────────────────
    // build_files()
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes each config entry from <paramref name="payload"/> to disk.
    /// Equivalent to Python <c>crossplane.build_files(payload, …)</c>.
    /// </summary>
    public static void BuildFiles(ParseResult payload, string? dirname = null, BuildOptions? options = null) =>
        new NginxBuilder().BuildFiles(payload, dirname, options);
}
