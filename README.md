# CrossplaneSharp

A C# port of the Python [crossplane](https://github.com/nginxinc/crossplane) library — a fast, reliable NGINX configuration file **lexer**, **parser**, and **builder** packaged as a .NET 10 NuGet library.

---

## Overview

CrossplaneSharp lets you work with NGINX configuration files programmatically in C#:

| Operation | What it does |
|-----------|-------------|
| **Lex** | Tokenise a config file into a stream of `(value, line, isQuoted)` tokens |
| **Parse** | Parse a config file (including `include` directives) into a structured object tree |
| **Build** | Reconstruct a valid NGINX config string from a parsed object tree |
| **BuildFiles** | Write a full parsed payload back to disk |

All three operations are exposed through the single static `Crossplane` class, matching the Python package's API one-to-one.

---

## Installation

```bash
dotnet add package CrossplaneSharp
```

---

## Quick Start

### Lex

Tokenise an NGINX config file into raw tokens:

```csharp
using CrossplaneSharp;

IReadOnlyList<NgxToken> tokens = Crossplane.Lex("/etc/nginx/nginx.conf");

foreach (var token in tokens)
    Console.WriteLine($"[line {token.Line}] {token.Value} (quoted={token.IsQuoted})");
```

Tokenise from an in-memory string:

```csharp
var tokens = Crossplane.LexString("worker_processes 4;");
// tokens[0].Value == "worker_processes"
// tokens[1].Value == "4"
// tokens[2].Value == ";"
```

---

### Parse

Parse a config file into a structured `ParseResult`:

```csharp
using CrossplaneSharp;

ParseResult result = Crossplane.Parse("/etc/nginx/nginx.conf");

Console.WriteLine(result.Status); // "ok" or "failed"

foreach (var config in result.Config)
{
    Console.WriteLine($"File: {config.File}");
    foreach (var stmt in config.Parsed)
        Console.WriteLine($"  [{stmt.Line}] {stmt.Directive} {string.Join(" ", stmt.Args)}");
}
```

#### Parse options

```csharp
var options = new ParseOptions
{
    CatchErrors = true,   // collect errors instead of throwing (default: true)
    Comments    = true,   // include # comment blocks in output (default: false)
    Single      = true,   // do not follow include directives (default: false)
    Strict      = false,  // raise on unknown directives (default: false)
    Combine     = false,  // flatten includes into one config entry (default: false)
    CheckCtx    = true,   // validate directive context (default: true)
    CheckArgs   = true,   // validate argument counts (default: true)
    Ignore      = new HashSet<string> { "lua_package_path" }, // skip these directives
    OnError     = ex => ex.Message   // optional error callback
};

ParseResult result = Crossplane.Parse("/etc/nginx/nginx.conf", options);
```

#### Output structure

```
ParseResult
├── Status          "ok" | "failed"
├── Errors[]        list of { Error, File, Line, Callback }
└── Config[]        one entry per parsed file
    ├── File        absolute path
    ├── Status      "ok" | "failed"
    ├── Errors[]
    └── Parsed[]    list of ConfigBlock
        ├── Directive   e.g. "server", "location", "#"
        ├── Line        1-based line number
        ├── Args[]      directive arguments
        ├── Block[]     child directives (for block directives)
        ├── Comment     comment text (when Directive == "#")
        ├── Includes[]  indices into Config[] (for include directives)
        └── File        source file (in combine mode)
```

---

### Build

Reconstruct an NGINX config string from a list of `ConfigBlock` objects:

```csharp
using CrossplaneSharp;

var blocks = new List<ConfigBlock>
{
    new() { Directive = "worker_processes", Args = ["4"] },
    new() {
        Directive = "events",
        Block = [
            new() { Directive = "worker_connections", Args = ["1024"] }
        ]
    }
};

string config = Crossplane.Build(blocks);
```

Output:
```nginx
worker_processes 4;
events {
    worker_connections 1024;
}
```

#### Build options

```csharp
var options = new BuildOptions
{
    Indent = 4,      // spaces per indent level (default: 4)
    Tabs   = false,  // use tabs instead of spaces (default: false)
    Header = true    // prepend a "built by crossplane" comment header (default: false)
};

string config = Crossplane.Build(blocks, options);
```

---

### BuildFiles

Write a full parsed payload back to disk (mirrors Python `crossplane.build_files()`):

```csharp
ParseResult result = Crossplane.Parse("/etc/nginx/nginx.conf");

// Rebuild all files into /tmp/nginx-out/
Crossplane.BuildFiles(result, dirname: "/tmp/nginx-out", new BuildOptions { Indent = 2 });
```

---

### Round-trip example

```csharp
// Parse → modify → rebuild
ParseResult result = Crossplane.Parse("/etc/nginx/nginx.conf",
    new ParseOptions { Comments = true });

// Find worker_processes and change its value
var wp = result.Config[0].Parsed.First(b => b.Directive == "worker_processes");
wp.Args[0] = "8";

string newConfig = Crossplane.Build(result.Config[0].Parsed);
File.WriteAllText("/etc/nginx/nginx.conf", newConfig);
```

---

## Error handling

By default errors are **collected** (not thrown) and stored in `ParseResult.Errors` and `ConfigFile.Errors`. Set `CatchErrors = false` to throw on the first error instead:

```csharp
try
{
    var result = Crossplane.Parse("broken.conf", new ParseOptions { CatchErrors = false });
}
catch (NgxParserSyntaxError ex)
{
    Console.WriteLine($"Syntax error at {ex.Filename}:{ex.Lineno} — {ex.Strerror}");
}
```

### Exception hierarchy

```
NgxParserBaseException
├── NgxParserSyntaxError              unbalanced braces, unexpected tokens
└── NgxParserDirectiveError
    ├── NgxParserDirectiveUnknownError    unknown directive (strict mode)
    ├── NgxParserDirectiveContextError    directive not allowed in this context
    └── NgxParserDirectiveArgumentsError  wrong number of arguments
```

---

## Project structure

```
CrossplaneSharp/
├── Crossplane.cs                 ← Static facade (Lex, Parse, Build, BuildFiles)
├── Lexer/
│   ├── NginxLexer.cs             ← Tokeniser (port of Python lexer.py)
│   └── NgxToken.cs               ← Token: Value, Line, IsQuoted
├── Parser/
│   ├── NginxParser.cs            ← Parser (port of Python parser.py)
│   ├── ConfigBlock.cs            ← Single directive / statement node
│   ├── ConfigFile.cs             ← Per-file parse result
│   ├── ParseResult.cs            ← Top-level payload
│   ├── ParseError.cs             ← Error entry
│   └── ParseOptions.cs           ← All parse options
├── Builder/
│   ├── NginxBuilder.cs           ← Builder (port of Python builder.py)
│   └── BuildOptions.cs           ← Build options (Indent, Tabs, Header)
├── Analyzer/
│   └── NginxAnalyzer.cs          ← Directive validation + full DIRECTIVES map
└── Exceptions/
    ├── NgxParserBaseException.cs
    ├── NgxParserSyntaxError.cs
    ├── NgxParserDirectiveError.cs
    ├── NgxParserDirectiveArgumentsError.cs
    ├── NgxParserDirectiveContextError.cs
    └── NgxParserDirectiveUnknownError.cs
```

---

## Requirements

- .NET 10.0+

