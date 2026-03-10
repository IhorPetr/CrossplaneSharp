// Based on crossplane (https://github.com/nginxinc/crossplane)
// Copyright 2019 NGINX, Inc. — Apache License 2.0
// C# port Copyright 2026 IhorPetr — Apache License 2.0

namespace CrossplaneSharp
{

    /// <summary>
    /// A single token produced by <see cref="NginxLexer"/>.
    /// Mirrors the Python crossplane 3-tuple <c>(token, lineno, quoted)</c>.
    /// </summary>
    public sealed class NgxToken
    {
        /// <summary>The raw text of the token.</summary>
        public string Value { get; }

        /// <summary>The 1-based line number where the token starts.</summary>
        public int Line { get; }

        /// <summary><c>true</c> if the token was enclosed in quotes.</summary>
        public bool IsQuoted { get; }

        public NgxToken(string value, int line, bool isQuoted)
        {
            Value = value;
            Line = line;
            IsQuoted = isQuoted;
        }

        public override string ToString() => $"NgxToken({Value}, line={Line}, quoted={IsQuoted})";
    }
}
