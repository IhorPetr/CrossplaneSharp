namespace CrossplaneSharp.Exceptions;

public class NgxParserDirectiveError : NgxParserBaseException
{
    public NgxParserDirectiveError(string strerror, string? filename, int? lineno)
        : base(strerror, filename, lineno) { }

    public NgxParserDirectiveError() : base() { }
}