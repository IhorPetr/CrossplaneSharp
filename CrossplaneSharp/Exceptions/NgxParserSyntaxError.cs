namespace CrossplaneSharp.Exceptions
{

    public class NgxParserSyntaxError : NgxParserBaseException
    {
        public NgxParserSyntaxError(string strerror, string filename, int? lineno)
            : base(strerror, filename, lineno) { }

        public NgxParserSyntaxError() : base() { }
    }
}
