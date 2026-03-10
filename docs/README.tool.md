# CrossplaneSharp.Tool

[![NuGet Tool](https://img.shields.io/nuget/v/CrossplaneSharp.Tool.svg?label=CrossplaneSharp.Tool)](https://www.nuget.org/packages/CrossplaneSharp.Tool)

A unofficial C# port of the Python [crossplane](https://github.com/nginxinc/crossplane) — a `crossplanesharp` CLI tool for parsing, lexing, building, formatting and minifying NGINX configuration files. Runs on Windows, Linux, and macOS.


---

## Installation

```bash
dotnet tool install -g CrossplaneSharp.Tool
```

> Requires **.NET 8+**. On Windows, file paths with backslashes are fully supported alongside forward-slash paths.

---

## Commands

```
crossplanesharp [command] [options]

Commands:
  parse    Parse an NGINX config file to a JSON payload
  build    Build NGINX config files from a JSON payload
  lex      Tokenise an NGINX config file to a JSON array
  minify   Remove all whitespace from an NGINX config
  format   Format an NGINX config file

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information
```

---

## parse

Parse an NGINX config file and output the result as JSON.

```bash
crossplanesharp parse <filename> [options]
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
crossplanesharp parse /etc/nginx/nginx.conf

# Indented output saved to file
crossplanesharp parse /etc/nginx/nginx.conf -i 4 -o payload.json

# Include comments, single file, strict mode
crossplanesharp parse nginx.conf --include-comments --single-file --strict

# Ignore specific directives
crossplanesharp parse nginx.conf --ignore lua_package_path,lua_package_cpath
```

---

## build

Build NGINX config files on disk from a JSON payload produced by `parse`.

```bash
crossplanesharp build <filename> [options]
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
crossplanesharp build payload.json -d /etc/nginx/

# Print to stdout instead of writing files
crossplanesharp build payload.json --stdout

# Force overwrite, tabs, no header, verbose
crossplanesharp build payload.json -f -t --no-headers -v
```

---

## lex

Tokenise an NGINX config file and output tokens as a JSON array.

```bash
crossplanesharp lex <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |
| `-i`, `--indent <num>` | Number of spaces to indent output |
| `-n`, `--line-numbers` | Include line numbers in JSON payload |

**Examples:**

```bash
# Tokenise to a flat JSON array
crossplanesharp lex /etc/nginx/nginx.conf

# Include line numbers, indented output
crossplanesharp lex /etc/nginx/nginx.conf -n -i 2
```

---

## minify

Remove all whitespace and comments from an NGINX config.

```bash
crossplanesharp minify <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |

**Examples:**

```bash
# Print minified config to stdout
crossplanesharp minify /etc/nginx/nginx.conf

# Save to file
crossplanesharp minify /etc/nginx/nginx.conf -o nginx.min.conf
```

---

## format

Format an NGINX config file with consistent indentation.

```bash
crossplanesharp format <filename> [options]
```

| Option | Description |
|--------|-------------|
| `-o`, `--out <path>` | Write output to a file |
| `-i`, `--indent <num>` | Spaces per indent level (default: 4) |
| `-t`, `--tabs` | Indent with tabs instead of spaces |

**Examples:**

```bash
# Format with 4-space indent (default)
crossplanesharp format /etc/nginx/nginx.conf

# Format with tabs, save to file
crossplanesharp format /etc/nginx/nginx.conf -t -o nginx.formatted.conf

# Format with 2-space indent
crossplanesharp format /etc/nginx/nginx.conf -i 2
```
