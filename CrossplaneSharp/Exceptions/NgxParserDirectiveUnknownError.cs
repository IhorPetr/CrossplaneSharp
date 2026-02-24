namespace CrossplaneSharp.Exceptions
{

public class NgxParserDirectiveUnknownError : NgxParserDirectiveError
{
    public NgxParserDirectiveUnknownError(string strerror, string filename, int? lineno)
        : base(strerror, filename, lineno) { }
}
}
