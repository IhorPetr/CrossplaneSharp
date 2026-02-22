namespace CrossplaneSharp;

/// <summary>
/// Controls the output format of <see cref="NginxBuilder.Build"/>.
/// </summary>
public class BuildOptions
{
    /// <summary>String used for each indentation level (default: four spaces).</summary>
    public string Indent { get; set; } = "    ";

    /// <summary>Emit comment blocks that were preserved during parsing.</summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>Append a trailing newline to the output.</summary>
    public bool Newline { get; set; } = true;
}
