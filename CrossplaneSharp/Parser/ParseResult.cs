namespace CrossplaneSharp;

/// <summary>
/// Top-level result returned by <see cref="NginxParser.Parse"/>.
/// Mirrors the Python crossplane <c>parse()</c> output structure.
/// </summary>
public class ParseResult
{
    public string Status { get; set; } = "ok";
    public List<ParseError> Errors { get; set; } = new();

    /// <summary>One entry per file that was parsed (the root file plus any includes).</summary>
    public List<ConfigFile> Config { get; set; } = new();
}
