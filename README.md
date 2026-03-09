# CrossplaneSharp

[![CI](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/IhorPetr/CrossplaneSharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/CrossplaneSharp.svg?label=CrossplaneSharp)](https://www.nuget.org/packages/CrossplaneSharp)
[![NuGet Tool](https://img.shields.io/nuget/v/CrossplaneSharp.Tool.svg?label=CrossplaneSharp.Tool)](https://www.nuget.org/packages/CrossplaneSharp.Tool)

A C# NGINX configuration file **lexer**, **parser**, and **builder** for .NET.

---

## Packages

| Package | Description | Docs |
|---------|-------------|------|
| [`CrossplaneSharp`](https://www.nuget.org/packages/CrossplaneSharp) | .NET Standard 2.0 library — use in your own projects | [docs/README.library.md](docs/README.library.md) |
| [`CrossplaneSharp.Tool`](https://www.nuget.org/packages/CrossplaneSharp.Tool) | `crossplanesharp` global CLI tool | [docs/README.tool.md](docs/README.tool.md) |

---

## Library — quick install

```bash
dotnet add package CrossplaneSharp
```

```csharp
using CrossplaneSharp;

// Lex
var tokens = Crossplane.Lex("/etc/nginx/nginx.conf");

// Parse
ParseResult result = Crossplane.Parse("/etc/nginx/nginx.conf");

// Build
string config = Crossplane.Build(result.Config[0].Parsed);
```

➡ **Full library documentation:** [docs/README.library.md](docs/README.library.md)

---

## CLI Tool — quick install

```bash
dotnet tool install -g CrossplaneSharp.Tool
```

```bash
crossplanesharp parse  /etc/nginx/nginx.conf -i 4
crossplanesharp lex    /etc/nginx/nginx.conf -n
crossplanesharp minify /etc/nginx/nginx.conf
crossplanesharp format /etc/nginx/nginx.conf
crossplanesharp build  payload.json -d /etc/nginx/
```

➡ **Full tool documentation:** [docs/README.tool.md](docs/README.tool.md)

---

## Cross-platform paths

The library uses `System.IO.Path` and `RuntimeInformation.IsOSPlatform` for all file path handling — no custom helpers. NGINX config `include` paths (which always use `/`) are normalised to the OS-native separator at runtime, so parsing works correctly on both Unix and Windows.

---

## Requirements

The library targets **`netstandard2.0`** (compatible with .NET 6+, .NET Framework 4.6.1+, .NET Core 2.0+, Mono, Xamarin, Unity).  
The CLI tool requires **.NET 8+**.
