namespace CrossplaneSharp
{

    /// <summary>
    /// Represents a non-fatal error collected during parsing.
    /// </summary>
    public class ParseError
    {
        public string Error { get; set; } = string.Empty;
        public string File { get; set; }
        public int? Line { get; set; }
        /// <summary>Optional value returned by <see cref="ParseOptions.OnError"/>.</summary>
        public object Callback { get; set; }
    }
}