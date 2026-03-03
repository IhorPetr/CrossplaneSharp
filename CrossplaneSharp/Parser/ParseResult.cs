using System.Collections.Generic;

namespace CrossplaneSharp
{

    /// <summary>
    /// Top-level result returned by <see cref="NginxParser.Parse"/>.
    /// </summary>
    public class ParseResult
    {
        public string Status { get; set; } = "ok";
        public List<ParseError> Errors { get; set; } = new List<ParseError>();

        /// <summary>One entry per file that was parsed (the root file plus any includes).</summary>
        public List<ConfigFile> Config { get; set; } = new List<ConfigFile>();
    }
}