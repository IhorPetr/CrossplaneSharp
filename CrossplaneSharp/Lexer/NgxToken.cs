namespace CrossplaneSharp;

/// <summary>
/// Represents a single token produced by the NGINX config lexer.
/// </summary>
/// <param name="Value">The raw string value of the token.</param>
/// <param name="Line">The 1-based line number where the token starts.</param>
/// <param name="IsQuoted">Whether the token was enclosed in quotes.</param>
public record NgxToken(string Value, int Line, bool IsQuoted);
