using System.Collections.Generic;

namespace CrossplaneSharp
{

    /// <summary>
    /// Represents a single directive (and optional nested block) in an NGINX config.
    /// Mirrors the Python crossplane statement dict: directive/line/args/block/comment/includes/file.
    /// </summary>
    public class ConfigBlock
    {
        /// <summary>The directive name, or "#" for a comment.</summary>
        public string Directive { get; set; } = string.Empty;

        /// <summary>1-based line number.</summary>
        public int Line { get; set; }

        /// <summary>Directive arguments.</summary>
        public List<string> Args { get; set; } = new List<string>();

        /// <summary>Child directives when this directive opens a block.</summary>
        public List<ConfigBlock> Block { get; set; }

        /// <summary>Comment text (without the leading <c>#</c>) when <see cref="Directive"/> is "#".</summary>
        public string Comment { get; set; }

        /// <summary>
        /// Indices into <see cref="ParseResult.Config"/> for files pulled in by
        /// an <c>include</c> directive (mirrors Python stmt['includes']).
        /// </summary>
        public List<int> Includes { get; set; }

        /// <summary>Source file path, populated in combine mode.</summary>
        public string File { get; set; }
    }
}