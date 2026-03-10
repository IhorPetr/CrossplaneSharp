// Based on crossplane (https://github.com/nginxinc/crossplane)
// Copyright 2019 NGINX, Inc. — Apache License 2.0
// C# port Copyright 2026 IhorPetr — Apache License 2.0

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