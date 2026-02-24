using System;
using System.Collections.Generic;

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
