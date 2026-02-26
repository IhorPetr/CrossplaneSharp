# CrossplaneSharp

[![CI (Ubuntu)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci-ubuntu.yml/badge.svg?branch=main)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci-ubuntu.yml)
[![CI (macOS)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci-macos.yml/badge.svg?branch=main)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci-macos.yml)
[![CI (Windows)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci-windows.yml/badge.svg?branch=main)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci-windows.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A C# port of the Python [crossplane](https://github.com/nginxinc/crossplane) library — a fast, reliable NGINX configuration file **lexer**, **parser**, and **builder** packaged as a .NET Standard 2.0 NuGet library.

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

The package targets **`netstandard2.0`** and works with .NET 6+, .NET Framework 4.6.1+, .NET Core 2.0+, Mono, Xamarin, and Unity.

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


## Requirements

The library targets **`netstandard2.0`**, so it is compatible with any runtime that supports .NET Standard 2.0:

| Runtime | Minimum version |
|---|---|
| .NET | 6.0+ |
| .NET Framework | 4.6.1+ |
| .NET Core | 2.0+ |
| Mono | 5.4+ |
| Xamarin.iOS | 10.14+ |
| Xamarin.Android | 8.0+ |
| Unity | 2018.1+ |

The compiler feature set uses **C# 10** (file-scoped namespaces, record types, nullable reference types).  
Any modern .NET SDK (6+) can build and consume the package regardless of which runtime you target.

