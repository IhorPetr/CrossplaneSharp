// Based on crossplane (https://github.com/nginxinc/crossplane)
// Copyright 2019 NGINX, Inc. — Apache License 2.0
// C# port Copyright 2026 IhorPetr — Apache License 2.0

using System.Collections.Generic;

namespace CrossplaneSharp
{

    /// <summary>
    /// Represents the parse result for a single NGINX config file.
    /// </summary>
    public class ConfigFile
    {
        public string File { get; set; } = string.Empty;
        public string Status { get; set; } = "ok";
        public List<ParseError> Errors { get; set; } = new List<ParseError>();
        public List<ConfigBlock> Parsed { get; set; } = new List<ConfigBlock>();
    }
}