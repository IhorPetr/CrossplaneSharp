using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrossplaneSharp;

// ─────────────────────────────────────────────────────────────────────────────
// crossplane-sharp  –  CLI tool for working with NGINX configuration files
// ─────────────────────────────────────────────────────────────────────────────

var rootCommand = new RootCommand("Various operations for NGINX config files.")
{
    Name = "crossplane-sharp"
};

rootCommand.SetHandler(() => { });

// ─────────────────────────────────────────────────────────────────────────────
// parse
// ─────────────────────────────────────────────────────────────────────────────
var parseCmd = new Command("parse", "Parse an NGINX config file to a JSON payload.");

var parseFile     = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
var parseOut      = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");
var parseIndent   = new Option<int?>(new[] { "-i", "--indent" }, "Number of spaces to indent output.");
var parseIgnore   = new Option<string>("--ignore", () => "", "Ignore directives (comma-separated).");
var parseNoCatch  = new Option<bool>("--no-catch", "Stop after the first error.");
var parseCombine  = new Option<bool>("--combine", "Flatten includes into one single config entry.");
var parseSingle   = new Option<bool>("--single-file", "Do not follow include directives.");
var parseComments = new Option<bool>("--include-comments", "Include comments in JSON output.");
var parseStrict   = new Option<bool>("--strict", "Raise errors for unknown directives.");

parseCmd.AddArgument(parseFile);
parseCmd.AddOption(parseOut);
parseCmd.AddOption(parseIndent);
parseCmd.AddOption(parseIgnore);
parseCmd.AddOption(parseNoCatch);
parseCmd.AddOption(parseCombine);
parseCmd.AddOption(parseSingle);
parseCmd.AddOption(parseComments);
parseCmd.AddOption(parseStrict);

parseCmd.SetHandler((InvocationContext ctx) =>
{
    var file     = ctx.ParseResult.GetValueForArgument(parseFile);
    var outFile  = ctx.ParseResult.GetValueForOption(parseOut);
    var indent   = ctx.ParseResult.GetValueForOption(parseIndent);
    var ignore   = ctx.ParseResult.GetValueForOption(parseIgnore) ?? "";
    var noCatch  = ctx.ParseResult.GetValueForOption(parseNoCatch);
    var combine  = ctx.ParseResult.GetValueForOption(parseCombine);
    var single   = ctx.ParseResult.GetValueForOption(parseSingle);
    var comments = ctx.ParseResult.GetValueForOption(parseComments);
    var strict   = ctx.ParseResult.GetValueForOption(parseStrict);

    var options = new ParseOptions
    {
        CatchErrors = !noCatch,
        Ignore      = new HashSet<string>(
                          ignore.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                          StringComparer.Ordinal),
        Single   = single,
        Comments = comments,
        Strict   = strict,
        Combine  = combine,
    };

    ParseResult result = Crossplane.Parse(file.FullName, options);
    WriteOutput(SerializeJson(result, indent ?? -1) + "\n", outFile?.FullName);
});

rootCommand.AddCommand(parseCmd);

// ─────────────────────────────────────────────────────────────────────────────
// build
// ─────────────────────────────────────────────────────────────────────────────
var buildCmd = new Command("build", "Build NGINX config files from a JSON payload.");

var buildFile    = new Argument<FileInfo>("filename", "The JSON payload file.") { Arity = ArgumentArity.ExactlyOne };
var buildVerbose = new Option<bool>(new[] { "-v", "--verbose" }, "Print paths of written files.");
var buildDir     = new Option<DirectoryInfo?>(new[] { "-d", "--dir" }, "Base directory to build in.");
var buildForce   = new Option<bool>(new[] { "-f", "--force" }, "Overwrite existing files without prompting.");
var buildIndent  = new Option<int>(new[] { "-i", "--indent" }, () => 4, "Spaces per indent level.");
var buildTabs    = new Option<bool>(new[] { "-t", "--tabs" }, "Indent with tabs instead of spaces.");
var buildNoHdr   = new Option<bool>("--no-headers", "Do not write the header comment to configs.");
var buildStdout  = new Option<bool>("--stdout", "Write configs to stdout instead of files.");

buildCmd.AddArgument(buildFile);
buildCmd.AddOption(buildVerbose);
buildCmd.AddOption(buildDir);
buildCmd.AddOption(buildForce);
buildCmd.AddOption(buildIndent);
buildCmd.AddOption(buildTabs);
buildCmd.AddOption(buildNoHdr);
buildCmd.AddOption(buildStdout);

buildCmd.SetHandler((InvocationContext ctx) =>
{
    var file      = ctx.ParseResult.GetValueForArgument(buildFile);
    var verbose   = ctx.ParseResult.GetValueForOption(buildVerbose);
    var dir       = ctx.ParseResult.GetValueForOption(buildDir);
    var force     = ctx.ParseResult.GetValueForOption(buildForce);
    var indent    = ctx.ParseResult.GetValueForOption(buildIndent);
    var tabs      = ctx.ParseResult.GetValueForOption(buildTabs);
    var noHeaders = ctx.ParseResult.GetValueForOption(buildNoHdr);
    var stdout    = ctx.ParseResult.GetValueForOption(buildStdout);

    string json = File.ReadAllText(file.FullName);
    ParseResult payload = DeserializeParseResult(json);
    string baseDir = dir?.FullName ?? Directory.GetCurrentDirectory();
    var opts = new BuildOptions { Indent = indent, Tabs = tabs, Header = !noHeaders };

    if (stdout)
    {
        foreach (var config in payload.Config)
        {
            string path   = ResolvePath(config.File, baseDir);
            string output = Crossplane.Build(config.Parsed, opts).TrimEnd() + "\n";
            Console.Write("# " + path + "\n" + output + "\n");
        }
        return;
    }

    if (!force)
    {
        var existing = payload.Config
            .Select(c => ResolvePath(c.File, baseDir))
            .Where(File.Exists)
            .ToList();

        if (existing.Count > 0)
        {
            Console.WriteLine($"building '{file.FullName}' would overwrite these files:");
            Console.WriteLine(string.Join("\n", existing));
            Console.Write("overwrite? (y/n [n]) ");
            string? answer = Console.ReadLine();
            if (answer == null || !answer.Trim().ToLowerInvariant().StartsWith("y"))
            {
                Console.WriteLine("not overwritten");
                return;
            }
        }
    }

    Crossplane.BuildFiles(payload, baseDir, opts);

    if (verbose)
        foreach (var config in payload.Config)
            Console.WriteLine("wrote to " + ResolvePath(config.File, baseDir));
});

rootCommand.AddCommand(buildCmd);

// ─────────────────────────────────────────────────────────────────────────────
// lex
// ─────────────────────────────────────────────────────────────────────────────
var lexCmd = new Command("lex", "Tokenise an NGINX config file to a JSON array.");

var lexFile    = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
var lexOut     = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");
var lexIndent  = new Option<int?>(new[] { "-i", "--indent" }, "Number of spaces to indent output.");
var lexLineNos = new Option<bool>(new[] { "-n", "--line-numbers" }, "Include line numbers in JSON.");

lexCmd.AddArgument(lexFile);
lexCmd.AddOption(lexOut);
lexCmd.AddOption(lexIndent);
lexCmd.AddOption(lexLineNos);

lexCmd.SetHandler(
    (FileInfo file, FileInfo? outFile, int? indent, bool lineNumbers) =>
    {
        var tokens = Crossplane.Lex(file.FullName);
        string json = lineNumbers
            ? SerializeJson(tokens.Select(t => new object[] { t.Value, t.Line }).ToList(), indent ?? -1)
            : SerializeJson(tokens.Select(t => t.Value).ToList(), indent ?? -1);
        WriteOutput(json + "\n", outFile?.FullName);
    },
    lexFile, lexOut, lexIndent, lexLineNos);

rootCommand.AddCommand(lexCmd);

// ─────────────────────────────────────────────────────────────────────────────
// minify
// ─────────────────────────────────────────────────────────────────────────────
var minifyCmd  = new Command("minify", "Remove all whitespace from an NGINX config.");
var minifyFile = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
var minifyOut  = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");

minifyCmd.AddArgument(minifyFile);
minifyCmd.AddOption(minifyOut);

minifyCmd.SetHandler(
    (FileInfo file, FileInfo? outFile) =>
    {
        var opts = new ParseOptions
        {
            Single = true, CatchErrors = false,
            CheckCtx = false, CheckArgs = false,
            Comments = false, Strict = false,
        };
        ParseResult payload = Crossplane.Parse(file.FullName, opts);
        WriteOutput(BuildMinified(payload.Config[0].Parsed) + "\n", outFile?.FullName);
    },
    minifyFile, minifyOut);

rootCommand.AddCommand(minifyCmd);

// ─────────────────────────────────────────────────────────────────────────────
// format
// ─────────────────────────────────────────────────────────────────────────────
var formatCmd    = new Command("format", "Format an NGINX config file.");
var formatFile   = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
var formatOut    = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");
var formatIndent = new Option<int>(new[] { "-i", "--indent" }, () => 4, "Spaces per indent level.");
var formatTabs   = new Option<bool>(new[] { "-t", "--tabs" }, "Indent with tabs instead of spaces.");

formatCmd.AddArgument(formatFile);
formatCmd.AddOption(formatOut);
formatCmd.AddOption(formatIndent);
formatCmd.AddOption(formatTabs);

formatCmd.SetHandler(
    (FileInfo file, FileInfo? outFile, int indent, bool tabs) =>
    {
        ParseResult payload = Crossplane.Parse(file.FullName, new ParseOptions
        {
            Comments = true, Single = true, CheckCtx = false, CheckArgs = false,
        });

        if (payload.Status != "ok")
        {
            var e = payload.Errors[0];
            Console.Error.WriteLine($"crossplane-sharp: error: {e.File ?? file.FullName}:{e.Line}: {e.Error}");
            Environment.Exit(1);
            return;
        }

        string output = Crossplane.Build(payload.Config[0].Parsed,
            new BuildOptions { Indent = indent, Tabs = tabs, Header = false });
        WriteOutput(output + "\n", outFile?.FullName);
    },
    formatFile, formatOut, formatIndent, formatTabs);

rootCommand.AddCommand(formatCmd);

// ─────────────────────────────────────────────────────────────────────────────
// Run
// ─────────────────────────────────────────────────────────────────────────────
return await rootCommand.InvokeAsync(args);

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static void WriteOutput(string content, string? outFile)
{
    if (outFile == null) Console.Write(content);
    else File.WriteAllText(outFile, content, Encoding.UTF8);
}

static string ResolvePath(string path, string baseDir) =>
    Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);

static string SerializeJson(object obj, int indent)
{
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented          = indent >= 0,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    string raw = JsonSerializer.Serialize(obj, jsonOptions);
    if (indent > 0 && indent != 2) raw = ReIndent(raw, indent);
    return raw;
}

static string ReIndent(string json, int spaces)
{
    string unit = new string(' ', spaces);
    var sb = new StringBuilder();
    int depth = 0;
    bool inString = false, escape = false;

    foreach (char ch in json)
    {
        if (escape) { sb.Append(ch); escape = false; continue; }
        if (ch == '\\' && inString) { sb.Append(ch); escape = true; continue; }
        if (ch == '"') { inString = !inString; sb.Append(ch); continue; }
        if (inString) { sb.Append(ch); continue; }

        switch (ch)
        {
            case '{': case '[':
                sb.Append(ch); sb.Append('\n'); depth++;
                sb.Append(string.Concat(Enumerable.Repeat(unit, depth))); break;
            case '}': case ']':
                sb.Append('\n'); depth--;
                sb.Append(string.Concat(Enumerable.Repeat(unit, depth)));
                sb.Append(ch); break;
            case ',':
                sb.Append(ch); sb.Append('\n');
                sb.Append(string.Concat(Enumerable.Repeat(unit, depth))); break;
            case ':':
                sb.Append(ch); sb.Append(' '); break;
            case ' ': case '\n': case '\r': case '\t':
                break;
            default:
                sb.Append(ch); break;
        }
    }
    return sb.ToString();
}

static ParseResult DeserializeParseResult(string json)
{
    var result = JsonSerializer.Deserialize<ParseResult>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (result == null) throw new InvalidOperationException("Failed to deserialize JSON payload.");
    return result;
}

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
        sb.Append(Enquote(stmt.Directive));
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
        else sb.Append(";");
    }
}

static string Enquote(string arg)
{
    if (string.IsNullOrEmpty(arg)) return "\"\"";
    if (arg.Any(c => char.IsWhiteSpace(c) || c == '{' || c == '}' || c == ';' || c == '"' || c == '\''))
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    return arg;
}



