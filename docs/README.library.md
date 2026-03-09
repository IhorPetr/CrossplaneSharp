# CrossplaneSharp — Library

[![NuGet](https://img.shields.io/nuget/v/CrossplaneSharp.svg?label=CrossplaneSharp)](https://www.nuget.org/packages/CrossplaneSharp)

A fast, reliable NGINX configuration file **lexer**, **parser**, and **builder** for .NET, packaged as a `netstandard2.0` NuGet library.

> **CLI Tool** — looking for the command-line tool? See [README.tool.md](README.tool.md) or the [`CrossplaneSharp.Tool`](https://www.nuget.org/packages/CrossplaneSharp.Tool) NuGet package.

---

## Installation

```bash
dotnet add package CrossplaneSharp
```

The package targets **`netstandard2.0`** and works with .NET 6+, .NET Framework 4.6.1+, .NET Core 2.0+, Mono, Xamarin, and Unity.

---

## Overview

| Operation | What it does |
|-----------|-------------|
| **Lex** | Tokenise a config file into a stream of `(value, line, isQuoted)` tokens |
| **Parse** | Parse a config file (including `include` directives) into a structured object tree |
| **Build** | Reconstruct a valid NGINX config string from a parsed object tree |
| **BuildFiles** | Write a full parsed payload back to disk |

All operations are exposed through the single static `Crossplane` class.

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
    Ignore      = new HashSet<string> { "lua_package_path" },
    OnError     = ex => ex.Message
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
    new ConfigBlock { Directive = "worker_processes", Args = new List<string> { "4" } },
    new ConfigBlock {
        Directive = "events",
        Block = new List<ConfigBlock> {
            new ConfigBlock { Directive = "worker_connections", Args = new List<string> { "1024" } }
        }
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

Write a full parsed payload back to disk:

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

## Cross-platform paths

The library uses `System.IO.Path` and `System.Runtime.InteropServices.RuntimeInformation` for all path handling — no custom helpers. NGINX config `include` directives always use forward-slash paths; the library normalises them to the OS-native separator at runtime so parsing works correctly on Windows, Linux, and macOS.

When writing tests against fixture files, use the same pattern:

```csharp
var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? "simple\\nginx.conf"
    : "simple/nginx.conf";

var result = Crossplane.Parse(Path.Combine(fixtureDir, filePath));
```

---

## Requirements

The library targets **`netstandard2.0`**:

| Runtime | Minimum version |
|---|---|
| .NET | 6.0+ |
| .NET Framework | 4.6.1+ |
| .NET Core | 2.0+ |
| Mono | 5.4+ |
| Xamarin.iOS | 10.14+ |
| Xamarin.Android | 8.0+ |
| Unity | 2018.1+ |

