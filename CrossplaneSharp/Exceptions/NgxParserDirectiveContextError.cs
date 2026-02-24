namespace CrossplaneSharp.Exceptions
{

public class NgxParserDirectiveContextError : NgxParserDirectiveError
{
    public NgxParserDirectiveContextError(string strerror, string filename, int? lineno)
        : base(strerror, filename, lineno) { }
}
}
