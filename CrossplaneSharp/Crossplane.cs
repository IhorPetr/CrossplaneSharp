namespace CrossplaneSharp;

/// <summary>
/// Top-level entry point for the CrossplaneSharp library.
/// Mirrors the three core functions of the Python crossplane package:
/// <c>crossplane.lex()</c>, <c>crossplane.parse()</c>, and <c>crossplane.build()</c>.
/// </summary>
public static class Crossplane
{
    /// <summary>
    /// Tokenises the NGINX config at <paramref name="filename"/> and returns a
    /// lazy sequence of <see cref="NgxToken"/> values.
    /// Equivalent to the Python <c>crossplane.lex(filename)</c>.
    /// </summary>
    public static IEnumerable<NgxToken> Lex(string filename) =>
        new NginxLexer().Tokenize(filename);

    /// <summary>
    /// Parses the NGINX config at <paramref name="filename"/> into a structured
    /// <see cref="ParseResult"/>.
    /// Equivalent to the Python <c>crossplane.parse(filename)</c>.
    /// </summary>
    public static ParseResult Parse(string filename, ParseOptions? options = null) =>
        new NginxParser().Parse(filename, options);

    /// <summary>
    /// Reconstructs an NGINX config string from <paramref name="directives"/>.
    /// Equivalent to the Python <c>crossplane.build(payload)</c>.
    /// </summary>
    public static string Build(IEnumerable<ConfigBlock> directives, BuildOptions? options = null) =>
        new NginxBuilder().Build(directives, options);
}
