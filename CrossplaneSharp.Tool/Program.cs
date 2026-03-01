using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CrossplaneSharp;

// ─────────────────────────────────────────────────────────────────────────────
// crossplane-sharp  –  C# port of the Python crossplane CLI
//
// Commands (mirrors python crossplane __main__.py):
//   parse    <file>  [options]   parse an nginx config → JSON
//   build    <file>  [options]   build nginx config files from a JSON payload
//   lex      <file>  [options]   tokenise an nginx config → JSON
//   minify   <file>  [options]   minify an nginx config
//   format   <file>  [options]   format an nginx config
// ─────────────────────────────────────────────────────────────────────────────

static string GetVersion() =>
    typeof(Program).Assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
                   // strip git-hash suffix added by the .NET SDK (e.g. "1.2.3+abc1234")
                   ?.Split('+')[0]
    ?? typeof(Program).Assembly.GetName().Version?.ToString()
    ?? "unknown";

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintMainHelp();
    return 0;
}

if (args[0] is "-V" or "--version")
{
    Console.WriteLine($"crossplane-sharp {GetVersion()}");
    return 0;
}

string command = args[0];
string[] rest = args.Skip(1).ToArray();

return command switch
{
    "parse"  => RunParse(rest),
    "build"  => RunBuild(rest),
    "lex"    => RunLex(rest),
    "minify" => RunMinify(rest),
    "format" => RunFormat(rest),
    "help"   => RunHelp(rest),
    _        => Error($"unknown command '{command}'. Run 'crossplane-sharp --help' for usage.")
};

// ─────────────────────────────────────────────────────────────────────────────
// parse
// ─────────────────────────────────────────────────────────────────────────────
static int RunParse(string[] args)
{
    // crossplane-sharp parse <filename>
    //   -o / --out          <path>        write output to a file
    //   -i / --indent       <num>         number of spaces to indent output
    //       --ignore        <directives>  comma-separated directives to ignore
    //       --no-catch                    stop after first error
    //       --combine                     flatten includes into one config
    //       --single-file                 do not follow include directives
    //       --include-comments            include comments in JSON
    //       --strict                      raise errors for unknown directives

    if (args.Length == 0 || IsHelp(args))
    {
        PrintParseHelp();
        return 0;
    }

    string filename = args[0];
    string? outFile = null;
    int indent = -1;          // -1 = compact
    string ignore = "";
    bool catchErrors = true;
    bool combine = false;
    bool single = false;
    bool comments = false;
    bool strict = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-o": case "--out":          outFile = args[++i]; break;
            case "-i": case "--indent":       indent = int.Parse(args[++i]); break;
            case "--ignore":                  ignore = args[++i]; break;
            case "--no-catch":                catchErrors = false; break;
            case "--combine":                 combine = true; break;
            case "--single-file":             single = true; break;
            case "--include-comments":        comments = true; break;
            case "--strict":                  strict = true; break;
            default: return Error($"unknown option '{args[i]}'");
        }
    }

    var options = new ParseOptions
    {
        CatchErrors = catchErrors,
        Ignore      = new HashSet<string>(
                          ignore.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries),
                          StringComparer.Ordinal),
        Single      = single,
        Comments    = comments,
        Strict      = strict,
        Combine     = combine,
    };

    ParseResult result = Crossplane.Parse(filename, options);

    string json = SerializeJson(result, indent);
    WriteOutput(json + "\n", outFile);
    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// build
// ─────────────────────────────────────────────────────────────────────────────
static int RunBuild(string[] args)
{
    // crossplane-sharp build <filename>
    //   -v / --verbose               verbose output (print written paths)
    //   -d / --dir     <path>        base directory to build in
    //   -f / --force                 overwrite existing files without prompting
    //   -i / --indent  <num>         spaces per indent level (default 4)
    //   -t / --tabs                  indent with tabs
    //       --no-headers             omit the "built by crossplane" header
    //       --stdout                 print to stdout instead of writing files

    if (args.Length == 0 || IsHelp(args))
    {
        PrintBuildHelp();
        return 0;
    }

    string filename = args[0];
    bool verbose = false;
    string? dirname = null;
    bool force = false;
    int indent = 4;
    bool tabs = false;
    bool header = true;
    bool stdout = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-v": case "--verbose":    verbose = true; break;
            case "-d": case "--dir":        dirname = args[++i]; break;
            case "-f": case "--force":      force = true; break;
            case "-i": case "--indent":     indent = int.Parse(args[++i]); break;
            case "-t": case "--tabs":       tabs = true; break;
            case "--no-headers":            header = false; break;
            case "--stdout":                stdout = true; break;
            default: return Error($"unknown option '{args[i]}'");
        }
    }

    string json = File.ReadAllText(filename);
    ParseResult payload = DeserializeParseResult(json);

    if (dirname == null)
        dirname = Directory.GetCurrentDirectory();

    var buildOptions = new BuildOptions { Indent = indent, Tabs = tabs, Header = header };

    // stdout mode: print each config to stdout  (mirrors Python --stdout)
    if (stdout)
    {
        foreach (var config in payload.Config)
        {
            string path = ResolvePath(config.File, dirname);
            string output = Crossplane.Build(config.Parsed, buildOptions).TrimEnd() + "\n";
            Console.Write("# " + path + "\n" + output + "\n");
        }
        return 0;
    }

    // check for existing files and ask unless --force
    if (!force)
    {
        var existing = payload.Config
            .Select(c => ResolvePath(c.File, dirname))
            .Where(File.Exists)
            .ToList();

        if (existing.Count > 0)
        {
            Console.WriteLine($"building '{filename}' would overwrite these files:");
            Console.WriteLine(string.Join("\n", existing));
            Console.Write("overwrite? (y/n [n]) ");
            string? answer = Console.ReadLine();
            if (answer == null || !answer.Trim().ToLowerInvariant().StartsWith("y"))
            {
                Console.WriteLine("not overwritten");
                return 0;
            }
        }
    }

    Crossplane.BuildFiles(payload, dirname, buildOptions);

    if (verbose)
    {
        foreach (var config in payload.Config)
            Console.WriteLine("wrote to " + ResolvePath(config.File, dirname));
    }

    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// lex
// ─────────────────────────────────────────────────────────────────────────────
static int RunLex(string[] args)
{
    // crossplane-sharp lex <filename>
    //   -o / --out            <path>   write output to a file
    //   -i / --indent         <num>    spaces to indent JSON output
    //   -n / --line-numbers            include line numbers in JSON

    if (args.Length == 0 || IsHelp(args))
    {
        PrintLexHelp();
        return 0;
    }

    string filename = args[0];
    string? outFile = null;
    int indent = -1;
    bool lineNumbers = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-o": case "--out":           outFile = args[++i]; break;
            case "-i": case "--indent":        indent = int.Parse(args[++i]); break;
            case "-n": case "--line-numbers":  lineNumbers = true; break;
            default: return Error($"unknown option '{args[i]}'");
        }
    }

    var tokens = Crossplane.Lex(filename);

    string json;
    if (lineNumbers)
    {
        var payload = tokens.Select(t => new object[] { t.Value, t.Line }).ToList();
        json = SerializeJson(payload, indent);
    }
    else
    {
        var payload = tokens.Select(t => t.Value).ToList();
        json = SerializeJson(payload, indent);
    }

    WriteOutput(json + "\n", outFile);
    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// minify
// ─────────────────────────────────────────────────────────────────────────────
static int RunMinify(string[] args)
{
    // crossplane-sharp minify <filename>
    //   -o / --out  <path>   write output to a file

    if (args.Length == 0 || IsHelp(args))
    {
        PrintMinifyHelp();
        return 0;
    }

    string filename = args[0];
    string? outFile = null;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-o": case "--out": outFile = args[++i]; break;
            default: return Error($"unknown option '{args[i]}'");
        }
    }

    // Parse with minimal checking (mirrors Python minify())
    var options = new ParseOptions
    {
        Single      = true,
        CatchErrors = false,
        CheckCtx    = false,
        CheckArgs   = false,
        Comments    = false,
        Strict      = false,
    };

    ParseResult payload = Crossplane.Parse(filename, options);
    string output = BuildMinified(payload.Config[0].Parsed) + "\n";
    WriteOutput(output, outFile);
    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// format
// ─────────────────────────────────────────────────────────────────────────────
static int RunFormat(string[] args)
{
    // crossplane-sharp format <filename>
    //   -o / --out      <path>   write output to a file
    //   -i / --indent   <num>    spaces per indent level (default 4)
    //   -t / --tabs              indent with tabs

    if (args.Length == 0 || IsHelp(args))
    {
        PrintFormatHelp();
        return 0;
    }

    string filename = args[0];
    string? outFile = null;
    int indent = 4;
    bool tabs = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-o": case "--out":    outFile = args[++i]; break;
            case "-i": case "--indent": indent = int.Parse(args[++i]); break;
            case "-t": case "--tabs":   tabs = true; break;
            default: return Error($"unknown option '{args[i]}'");
        }
    }

    // Parse with comments, no include follow, no validation (mirrors Python format())
    var parseOptions = new ParseOptions
    {
        Comments  = true,
        Single    = true,
        CheckCtx  = false,
        CheckArgs = false,
    };

    ParseResult payload = Crossplane.Parse(filename, parseOptions);

    if (payload.Status != "ok")
    {
        var e = payload.Errors[0];
        return Error($"{e.File ?? filename}:{e.Line}: {e.Error}");
    }

    var buildOptions = new BuildOptions { Indent = indent, Tabs = tabs, Header = false };
    string output = Crossplane.Build(payload.Config[0].Parsed, buildOptions);
    WriteOutput(output + "\n", outFile);
    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// help
// ─────────────────────────────────────────────────────────────────────────────
static int RunHelp(string[] args)
{
    if (args.Length == 0)
    {
        PrintMainHelp();
        return 0;
    }

    switch (args[0])
    {
        case "parse":  PrintParseHelp();  break;
        case "build":  PrintBuildHelp();  break;
        case "lex":    PrintLexHelp();    break;
        case "minify": PrintMinifyHelp(); break;
        case "format": PrintFormatHelp(); break;
        default: return Error($"unknown command '{args[0]}'");
    }
    return 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static bool IsHelp(string[] args) =>
    args.Contains("-h") || args.Contains("--help");

static int Error(string message)
{
    Console.Error.WriteLine($"crossplane-sharp: error: {message}");
    return 1;
}

static void WriteOutput(string content, string? outFile)
{
    if (outFile == null)
    {
        Console.Write(content);
    }
    else
    {
        File.WriteAllText(outFile, content, Encoding.UTF8);
    }
}

static string ResolvePath(string path, string baseDir)
{
    if (Path.IsPathRooted(path))
        return path;
    return Path.Combine(baseDir, path);
}

static string SerializeJson(object obj, int indent)
{
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented         = indent >= 0,
        PropertyNamingPolicy  = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    string raw = JsonSerializer.Serialize(obj, jsonOptions);

    // If a specific indent width other than 2 (the .NET default) was requested,
    // re-indent using the requested number of spaces.
    if (indent > 0 && indent != 2)
        raw = ReIndent(raw, indent);

    return raw;
}

/// <summary>Re-indents JSON that was serialised with 2-space indent.</summary>
static string ReIndent(string json, int spaces)
{
    string unit = new string(' ', spaces);
    var sb = new StringBuilder();
    int depth = 0;
    bool inString = false;
    bool escape = false;

    foreach (char ch in json)
    {
        if (escape) { sb.Append(ch); escape = false; continue; }
        if (ch == '\\' && inString) { sb.Append(ch); escape = true; continue; }
        if (ch == '"') { inString = !inString; sb.Append(ch); continue; }
        if (inString) { sb.Append(ch); continue; }

        switch (ch)
        {
            case '{': case '[':
                sb.Append(ch);
                sb.Append('\n');
                depth++;
                sb.Append(string.Concat(Enumerable.Repeat(unit, depth)));
                break;
            case '}': case ']':
                sb.Append('\n');
                depth--;
                sb.Append(string.Concat(Enumerable.Repeat(unit, depth)));
                sb.Append(ch);
                break;
            case ',':
                sb.Append(ch);
                sb.Append('\n');
                sb.Append(string.Concat(Enumerable.Repeat(unit, depth)));
                break;
            case ':':
                sb.Append(ch);
                sb.Append(' ');
                break;
            case ' ': case '\n': case '\r': case '\t':
                // strip whitespace outside strings (we re-produce it ourselves)
                break;
            default:
                sb.Append(ch);
                break;
        }
    }
    return sb.ToString();
}

static ParseResult DeserializeParseResult(string json)
{
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };
    var result = JsonSerializer.Deserialize<ParseResult>(json, jsonOptions);
    if (result == null)
        throw new InvalidOperationException("Failed to deserialize JSON payload.");
    return result;
}

/// <summary>
/// Minify a list of ConfigBlocks into a compact single-line string.
/// Mirrors the Python crossplane minify() write_block() function.
/// </summary>
static string BuildMinified(List<ConfigBlock> block)
{
    var sb = new StringBuilder();
    WriteMinifiedBlock(sb, block);
    return sb.ToString();
}

static void WriteMinifiedBlock(StringBuilder sb, List<ConfigBlock> block)
{
    foreach (var stmt in block)
    {
        string directive = Enquote(stmt.Directive);
        sb.Append(directive);

        if (stmt.Directive == "if")
        {
            sb.Append(" (");
            sb.Append(string.Join(" ", (stmt.Args ?? new List<string>()).Select(Enquote)));
            sb.Append(")");
        }
        else if (stmt.Args != null && stmt.Args.Count > 0)
        {
            sb.Append(" ");
            sb.Append(string.Join(" ", stmt.Args.Select(Enquote)));
        }

        if (stmt.Block != null)
        {
            sb.Append("{");
            WriteMinifiedBlock(sb, stmt.Block);
            sb.Append("}");
        }
        else
        {
            sb.Append(";");
        }
    }
}

/// <summary>
/// Wrap a value in quotes if it contains whitespace or NGINX special chars.
/// Mirrors Python crossplane builder._enquote().
/// </summary>
static string Enquote(string arg)
{
    if (string.IsNullOrEmpty(arg)) return "\"\"";
    if (arg.Any(c => char.IsWhiteSpace(c) || c == '{' || c == '}' || c == ';' || c == '"' || c == '\''))
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    return arg;
}

// ─────────────────────────────────────────────────────────────────────────────
// Help text (mirrors Python argparse output style)
// ─────────────────────────────────────────────────────────────────────────────

static void PrintMainHelp()
{
    Console.WriteLine($"crossplane-sharp {GetVersion()}");
    Console.WriteLine();
    Console.WriteLine("usage: crossplane-sharp <command> [options]");
    Console.WriteLine();
    Console.WriteLine("various operations for nginx config files");
    Console.WriteLine();
    Console.WriteLine("commands:");
    Console.WriteLine("  parse    parses a json payload for an nginx config");
    Console.WriteLine("  build    builds an nginx config from a json payload");
    Console.WriteLine("  lex      lexes tokens from an nginx config file");
    Console.WriteLine("  minify   removes all whitespace from an nginx config");
    Console.WriteLine("  format   formats an nginx config file");
    Console.WriteLine("  help     show help for commands");
    Console.WriteLine();
    Console.WriteLine("options:");
    Console.WriteLine("  -h, --help     show this help message and exit");
    Console.WriteLine("  -V, --version  show program version and exit");
}

static void PrintParseHelp()
{
    Console.WriteLine("usage: crossplane-sharp parse <filename> [options]");
    Console.WriteLine();
    Console.WriteLine("parses a json payload for an nginx config");
    Console.WriteLine();
    Console.WriteLine("positional arguments:");
    Console.WriteLine("  filename                  the nginx config file");
    Console.WriteLine();
    Console.WriteLine("options:");
    Console.WriteLine("  -h, --help                show this help message and exit");
    Console.WriteLine("  -o, --out <path>          write output to a file");
    Console.WriteLine("  -i, --indent <num>        number of spaces to indent output");
    Console.WriteLine("  --ignore <directives>     ignore directives (comma-separated)");
    Console.WriteLine("  --no-catch                only collect first error in file");
    Console.WriteLine("  --combine                 use includes to create one single file");
    Console.WriteLine("  --single-file             do not include other config files");
    Console.WriteLine("  --include-comments        include comments in json");
    Console.WriteLine("  --strict                  raise errors for unknown directives");
}

static void PrintBuildHelp()
{
    Console.WriteLine("usage: crossplane-sharp build <filename> [options]");
    Console.WriteLine();
    Console.WriteLine("builds an nginx config from a json payload");
    Console.WriteLine();
    Console.WriteLine("positional arguments:");
    Console.WriteLine("  filename                  the file with the config payload (JSON)");
    Console.WriteLine();
    Console.WriteLine("options:");
    Console.WriteLine("  -h, --help                show this help message and exit");
    Console.WriteLine("  -v, --verbose             verbose output");
    Console.WriteLine("  -d, --dir <path>          the base directory to build in");
    Console.WriteLine("  -f, --force               overwrite existing files");
    Console.WriteLine("  -i, --indent <num>        number of spaces to indent output (default 4)");
    Console.WriteLine("  -t, --tabs                indent with tabs instead of spaces");
    Console.WriteLine("  --no-headers              do not write header to configs");
    Console.WriteLine("  --stdout                  write configs to stdout instead");
}

static void PrintLexHelp()
{
    Console.WriteLine("usage: crossplane-sharp lex <filename> [options]");
    Console.WriteLine();
    Console.WriteLine("lexes tokens from an nginx config file");
    Console.WriteLine();
    Console.WriteLine("positional arguments:");
    Console.WriteLine("  filename                  the nginx config file");
    Console.WriteLine();
    Console.WriteLine("options:");
    Console.WriteLine("  -h, --help                show this help message and exit");
    Console.WriteLine("  -o, --out <path>          write output to a file");
    Console.WriteLine("  -i, --indent <num>        number of spaces to indent output");
    Console.WriteLine("  -n, --line-numbers        include line numbers in json payload");
}

static void PrintMinifyHelp()
{
    Console.WriteLine("usage: crossplane-sharp minify <filename> [options]");
    Console.WriteLine();
    Console.WriteLine("removes all whitespace from an nginx config");
    Console.WriteLine();
    Console.WriteLine("positional arguments:");
    Console.WriteLine("  filename                  the nginx config file");
    Console.WriteLine();
    Console.WriteLine("options:");
    Console.WriteLine("  -h, --help                show this help message and exit");
    Console.WriteLine("  -o, --out <path>          write output to a file");
}

static void PrintFormatHelp()
{
    Console.WriteLine("usage: crossplane-sharp format <filename> [options]");
    Console.WriteLine();
    Console.WriteLine("formats an nginx config file");
    Console.WriteLine();
    Console.WriteLine("positional arguments:");
    Console.WriteLine("  filename                  the nginx config file");
    Console.WriteLine();
    Console.WriteLine("options:");
    Console.WriteLine("  -h, --help                show this help message and exit");
    Console.WriteLine("  -o, --out <path>          write output to a file");
    Console.WriteLine("  -i, --indent <num>        number of spaces to indent output (default 4)");
    Console.WriteLine("  -t, --tabs                indent with tabs instead of spaces");
}


