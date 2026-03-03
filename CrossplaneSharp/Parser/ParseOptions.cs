using System;
using System.Collections.Generic;

namespace CrossplaneSharp
{

    /// <summary>
    /// Controls the behaviour of <see cref="NginxParser.Parse"/>.
    /// </summary>
    public class ParseOptions
    {
        /// <summary>
        /// Collect errors and continue rather than throwing on the first error.
        /// Default: <c>true</c>.
        /// </summary>
        public bool CatchErrors { get; set; } = true;

        /// <summary>
        /// Directives to exclude from the parse output.
        /// </summary>
        public ISet<string> Ignore { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// When <c>true</c>, <c>include</c> directives are not followed.
        /// Default: <c>false</c>.
        /// </summary>
        public bool Single { get; set; } = false;

        /// <summary>
        /// Preserve <c>#</c> comment tokens as <see cref="ConfigBlock"/> entries.
        /// Default: <c>false</c>.
        /// </summary>
        public bool Comments { get; set; } = false;

        /// <summary>
        /// Raise an error for unrecognised directives.
        /// Default: <c>false</c>.
        /// </summary>
        public bool Strict { get; set; } = false;

        /// <summary>
        /// Flatten all included files into a single config entry.
        /// Default: <c>false</c>.
        /// </summary>
        public bool Combine { get; set; } = false;

        /// <summary>
        /// Run context validation on each directive.
        /// Default: <c>true</c>.
        /// </summary>
        public bool CheckCtx { get; set; } = true;

        /// <summary>
        /// Run argument-count validation on each directive.
        /// Default: <c>true</c>.
        /// </summary>
        public bool CheckArgs { get; set; } = true;

        /// <summary>
        /// Optional callback invoked for every error.
        /// The return value is stored in <see cref="ParseError.Callback"/>.
        /// </summary>
        public Func<Exception, object> OnError { get; set; }
    }
}