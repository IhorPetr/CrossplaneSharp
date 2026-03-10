// Based on crossplane (https://github.com/nginxinc/crossplane)
// Copyright 2019 NGINX, Inc. — Apache License 2.0
// C# port Copyright 2026 IhorPetr — Apache License 2.0

namespace CrossplaneSharp.Exceptions
{

    public class NgxParserDirectiveError : NgxParserBaseException
    {
        public NgxParserDirectiveError(string strerror, string filename, int? lineno)
            : base(strerror, filename, lineno) { }

        public NgxParserDirectiveError() : base() { }
    }
}
