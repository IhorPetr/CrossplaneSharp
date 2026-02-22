namespace CrossplaneSharp;

/// <summary>
/// Represents a single directive (and optional nested block) in an NGINX config.
/// </summary>
public class ConfigBlock
{
    public string Directive { get; set; } = string.Empty;
    public int Line { get; set; }
    public List<string> Args { get; set; } = new();

    /// <summary>Child directives when this directive opens a block (e.g. <c>server { … }</c>).</summary>
    public List<ConfigBlock>? Block { get; set; }

    /// <summary>Inline comment text (without the leading <c>#</c>).</summary>
    public string? Comment { get; set; }

    /// <summary>Parsed contents of files pulled in by an <c>include</c> directive.</summary>
    public List<ConfigFile>? Includes { get; set; }
}
