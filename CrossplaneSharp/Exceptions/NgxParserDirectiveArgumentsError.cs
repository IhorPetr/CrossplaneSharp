namespace CrossplaneSharp.Exceptions;

public class NgxParserDirectiveArgumentsError : NgxParserDirectiveError
{
    public NgxParserDirectiveArgumentsError(string strerror, string? filename, int? lineno)
        : base(strerror, filename, lineno) { }
}