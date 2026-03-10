// Based on crossplane (https://github.com/nginxinc/crossplane)
// Copyright 2019 NGINX, Inc. — Apache License 2.0
// C# port Copyright 2026 IhorPetr — Apache License 2.0

using System;

namespace CrossplaneSharp.Exceptions
{

    public class NgxParserBaseException : Exception
    {
        public string Strerror { get; }
        public string Filename { get; }
        public int? Lineno { get; }

        public NgxParserBaseException(string strerror, string filename, int? lineno)
            : base(lineno.HasValue
                ? $"{strerror} in {filename}:{lineno}"
                : $"{strerror} in {filename}")
        {
            Strerror = strerror;
            Filename = filename;
            Lineno = lineno;
        }

        // Parameterless overload kept for compat with legacy call sites
        public NgxParserBaseException() : base() { Strerror = ""; }
    }
}
