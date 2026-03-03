# CrossplaneSharp.Tool

[![NuGet Tool](https://img.shields.io/nuget/v/CrossplaneSharp.Tool.svg?label=CrossplaneSharp.Tool)](https://www.nuget.org/packages/CrossplaneSharp.Tool)

A `crossplane-sharp` CLI tool for parsing, lexing, building, formatting and minifying NGINX configuration files.

> **Library** — looking for the C# library? See [README.library.md](README.library.md) or the [`CrossplaneSharp`](https://www.nuget.org/packages/CrossplaneSharp) NuGet package.

---

## Installation

```bash
dotnet tool install -g CrossplaneSharp.Tool
```

---

## Commands

```
crossplane-sharp [command] [options]

Commands:
  parse    Parse an NGINX config file to a JSON payload
  build    Build NGINX config files from a JSON payload
  lex      Tokenise an NGINX config file to a JSON array
  minify   Remove all whitespace from an NGINX config
  format   Format an NGINX config file

Options:
  --version   Show version information
  -?, -h, --help  Show help and usage information
```

---

## parse

Parse an NGINX config file and output the result as JSON.

```bash
crossplane-sharp parse <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |
| `-i`, `--indent <num>` | Number of spaces to indent output |
| `--ignore <directives>` | Ignore directives (comma-separated) |
| `--no-catch` | Stop after the first error |
| `--combine` | Flatten includes into one single config entry |
| `--single-file` | Do not follow include directives |
| `--include-comments` | Include comments in JSON output |
| `--strict` | Raise errors for unknown directives |

**Examples:**

```bash
# Parse to JSON (stdout)
crossplane-sharp parse /etc/nginx/nginx.conf

# Indented output saved to file
crossplane-sharp parse /etc/nginx/nginx.conf -i 4 -o payload.json

# Include comments, single file, strict mode
crossplane-sharp parse nginx.conf --include-comments --single-file --strict

# Ignore specific directives
crossplane-sharp parse nginx.conf --ignore lua_package_path,lua_package_cpath
```

---

## build

Build NGINX config files on disk from a JSON payload produced by `parse`.

```bash
crossplane-sharp build <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-v`, `--verbose` | Print paths of written files |
| `-d`, `--dir <path>` | Base directory to build in |
| `-f`, `--force` | Overwrite existing files without prompting |
| `-i`, `--indent <num>` | Spaces per indent level (default: 4) |
| `-t`, `--tabs` | Indent with tabs instead of spaces |
| `--no-headers` | Do not write the header comment to configs |
| `--stdout` | Write configs to stdout instead of files |

**Examples:**

```bash
# Build files into a directory
crossplane-sharp build payload.json -d /etc/nginx/

# Print to stdout instead of writing files
crossplane-sharp build payload.json --stdout

# Force overwrite, tabs, no header, verbose
crossplane-sharp build payload.json -f -t --no-headers -v
```

---

## lex

Tokenise an NGINX config file and output tokens as a JSON array.

```bash
crossplane-sharp lex <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |
| `-i`, `--indent <num>` | Number of spaces to indent output |
| `-n`, `--line-numbers` | Include line numbers in JSON payload |

**Examples:**

```bash
# Tokenise to a flat JSON array
crossplane-sharp lex /etc/nginx/nginx.conf

# Include line numbers, indented output
crossplane-sharp lex /etc/nginx/nginx.conf -n -i 2
```

---

## minify

Remove all whitespace and comments from an NGINX config.

```bash
crossplane-sharp minify <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |

**Examples:**

```bash
# Print minified config to stdout
crossplane-sharp minify /etc/nginx/nginx.conf

# Save to file
crossplane-sharp minify /etc/nginx/nginx.conf -o nginx.min.conf
```

---

## format

Format an NGINX config file with consistent indentation.

```bash
crossplane-sharp format <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |
| `-i`, `--indent <num>` | Spaces per indent level (default: 4) |
| `-t`, `--tabs` | Indent with tabs instead of spaces |

**Examples:**

```bash
# Format with 4-space indent (default)
crossplane-sharp format /etc/nginx/nginx.conf

# Format with tabs, save to file
crossplane-sharp format /etc/nginx/nginx.conf -t -o nginx.formatted.conf

# Format with 2-space indent
crossplane-sharp format /etc/nginx/nginx.conf -i 2
```

