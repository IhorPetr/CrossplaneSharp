namespace CrossplaneSharp;

/// <summary>
/// Represents a non-fatal error collected during parsing.
/// </summary>
public class ParseError
{
    public string Error { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
}
