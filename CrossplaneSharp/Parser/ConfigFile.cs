namespace CrossplaneSharp;

/// <summary>
/// Represents the parse result for a single NGINX config file.
/// </summary>
public class ConfigFile
{
    public string File { get; set; } = string.Empty;
    public string Status { get; set; } = "ok";
    public List<ParseError> Errors { get; set; } = new();
    public List<ConfigBlock> Parsed { get; set; } = new();
}
