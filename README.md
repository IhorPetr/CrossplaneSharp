# CrossplaneSharp

[![CI](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/CrossplaneSharp.svg?label=CrossplaneSharp)](https://www.nuget.org/packages/CrossplaneSharp)
[![NuGet Tool](https://img.shields.io/nuget/v/CrossplaneSharp.Tool.svg?label=CrossplaneSharp.Tool)](https://www.nuget.org/packages/CrossplaneSharp.Tool)

A Unofficial C# port of the Python [crossplane](https://github.com/nginxinc/crossplane) library — a fast, reliable NGINX configuration file **lexer**, **parser**, and **builder** packaged as a .NET Standard 2.0 NuGet library.

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

## CLI Tool

A standalone `crossplane-sharp` CLI tool is also available as a separate NuGet package:

```bash
dotnet tool install -g CrossplaneSharp.Tool
```

The tool mirrors all five subcommands of the Python `crossplane` CLI:

```
crossplane-sharp <command> [options]

commands:
  parse    parses a json payload for an nginx config
  build    builds an nginx config from a json payload
  lex      lexes tokens from an nginx config file
  minify   removes all whitespace from an nginx config
  format   formats an nginx config file
  help     show help for commands
```

### parse

```bash
# Parse to JSON (stdout)
crossplane-sharp parse /etc/nginx/nginx.conf

# Indented output, save to file
crossplane-sharp parse /etc/nginx/nginx.conf -i 4 -o payload.json

# Include comments, don't follow includes, strict mode
crossplane-sharp parse nginx.conf --include-comments --single-file --strict

# All options:
#   -o / --out <path>         write output to a file
#   -i / --indent <num>       number of spaces to indent output
#       --ignore <directives> ignore directives (comma-separated)
#       --no-catch            stop after first error
#       --combine             flatten includes into one config
#       --single-file         do not follow include directives
#       --include-comments    include comments in JSON
#       --strict              raise errors for unknown directives
```

### build

```bash
# Build config files from a JSON payload onto disk
crossplane-sharp build payload.json -d /etc/nginx/

# Print to stdout instead of writing files
crossplane-sharp build payload.json --stdout

# Force overwrite, use tabs, omit header, verbose
crossplane-sharp build payload.json -f -t --no-headers -v

# All options:
#   -v / --verbose            verbose output (print written paths)
#   -d / --dir <path>         base directory to build in
#   -f / --force              overwrite existing files without prompting
#   -i / --indent <num>       spaces per indent level (default 4)
#   -t / --tabs               indent with tabs
#       --no-headers          omit the "built by crossplane" header
#       --stdout              print to stdout instead of writing files
```

### lex

```bash
# Tokenise to a JSON array
crossplane-sharp lex /etc/nginx/nginx.conf

# Include line numbers
crossplane-sharp lex /etc/nginx/nginx.conf -n -i 2

# All options:
#   -o / --out <path>         write output to a file
#   -i / --indent <num>       number of spaces to indent output
#   -n / --line-numbers       include line numbers in JSON
```

### minify

```bash
# Strip all whitespace / comments
crossplane-sharp minify /etc/nginx/nginx.conf

# Save to file
crossplane-sharp minify /etc/nginx/nginx.conf -o nginx.min.conf
```

### format

```bash
# Format with 4-space indent (default)
crossplane-sharp format /etc/nginx/nginx.conf

# Format with tabs, save to file
crossplane-sharp format /etc/nginx/nginx.conf -t -o nginx.formatted.conf
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


