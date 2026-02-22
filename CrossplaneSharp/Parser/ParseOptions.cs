namespace CrossplaneSharp;

/// <summary>
/// Controls the behaviour of <see cref="NginxParser.Parse"/>.
/// Mirrors every parameter of the Python crossplane <c>parse()</c> function.
/// </summary>
public class ParseOptions
{
    /// <summary>
    /// Collect errors and continue rather than throwing on the first error.
    /// Equivalent to Python <c>catch_errors</c>. Default: <c>true</c>.
    /// </summary>
    public bool CatchErrors { get; set; } = true;

    /// <summary>
    /// Directives to exclude from the parse output.
    /// Equivalent to Python <c>ignore</c>.
    /// </summary>
    public ISet<string> Ignore { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// When <c>true</c>, <c>include</c> directives are not followed.
    /// Equivalent to Python <c>single</c>. Default: <c>false</c>.
    /// </summary>
    public bool Single { get; set; } = false;

    /// <summary>
    /// Preserve <c>#</c> comment tokens as <see cref="ConfigBlock"/> entries.
    /// Equivalent to Python <c>comments</c>. Default: <c>false</c>.
    /// </summary>
    public bool Comments { get; set; } = false;

    /// <summary>
    /// Raise an error for unrecognised directives.
    /// Equivalent to Python <c>strict</c>. Default: <c>false</c>.
    /// </summary>
    public bool Strict { get; set; } = false;

    /// <summary>
    /// Flatten all included files into a single config entry.
    /// Equivalent to Python <c>combine</c>. Default: <c>false</c>.
    /// </summary>
    public bool Combine { get; set; } = false;

    /// <summary>
    /// Run context validation on each directive.
    /// Equivalent to Python <c>check_ctx</c>. Default: <c>true</c>.
    /// </summary>
    public bool CheckCtx { get; set; } = true;

    /// <summary>
    /// Run argument-count validation on each directive.
    /// Equivalent to Python <c>check_args</c>. Default: <c>true</c>.
    /// </summary>
    public bool CheckArgs { get; set; } = true;

    /// <summary>
    /// Optional callback invoked for every error.
    /// The return value is stored in <see cref="ParseError.Callback"/>.
    /// Equivalent to Python <c>onerror</c>.
    /// </summary>
    public Func<Exception, object?>? OnError { get; set; }
}
