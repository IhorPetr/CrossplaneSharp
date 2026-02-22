namespace CrossplaneSharp;

/// <summary>
/// Controls the behaviour of <see cref="NginxParser.Parse"/>.
/// </summary>
public class ParseOptions
{
    /// <summary>Recursively parse files referenced by <c>include</c> directives.</summary>
    public bool ParseIncludes { get; set; } = true;

    /// <summary>
    /// Collect errors and continue rather than throwing on the first syntax error.
    /// When <c>false</c> the first error raises an exception.
    /// </summary>
    public bool CatchErrors { get; set; } = true;

    /// <summary>Preserve <c>#</c> comment tokens as <see cref="ConfigBlock"/> entries.</summary>
    public bool Comments { get; set; } = false;

    /// <summary>
    /// Directives whose parse errors should be ignored (e.g. unknown third-party directives).
    /// </summary>
    public ISet<string> Ignore { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
