using CrossplaneSharp.Exceptions;

namespace CrossplaneSharp;

/// <summary>
/// Parses one or more NGINX configuration files into a structured
/// <see cref="ParseResult"/>.  Equivalent to the Python crossplane
/// <c>parse()</c> function.
/// </summary>
public class NginxParser
{
    private readonly NginxLexer _lexer = new();

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the NGINX config at <paramref name="filename"/>.
    /// </summary>
    public ParseResult Parse(string filename, ParseOptions? options = null)
    {
        options ??= new ParseOptions();
        var result = new ParseResult();
        ParseFile(filename, result, options);
        return result;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void ParseFile(string filename, ParseResult result, ParseOptions options)
    {
        var configFile = new ConfigFile { File = filename };

        try
        {
            IEnumerable<NgxToken> tokens = _lexer.Tokenize(filename);
            using var enumerator = tokens.GetEnumerator();

            ParseBlock(enumerator, configFile, result, options, filename, depth: 0);
        }
        catch (Exception ex) when (ex is not NgxParserBaseException || options.CatchErrors)
        {
            var error = new ParseError
            {
                Error = ex.Message,
                File = filename,
                Line = 0
            };
            configFile.Errors.Add(error);
            result.Errors.Add(error);
            configFile.Status = "failed";
            result.Status = "failed";
        }

        result.Config.Add(configFile);
    }

    /// <summary>
    /// Parses a sequence of directives from <paramref name="enumerator"/> until
    /// the end of the token stream or a closing <c>}</c> is encountered.
    /// </summary>
    private void ParseBlock(
        IEnumerator<NgxToken> enumerator,
        ConfigFile configFile,
        ParseResult result,
        ParseOptions options,
        string filename,
        int depth)
    {
        while (enumerator.MoveNext())
        {
            NgxToken token = enumerator.Current;

            // ── Comment ───────────────────────────────────────────────────────
            if (token.Value.StartsWith('#'))
            {
                if (options.Comments)
                {
                    configFile.Parsed.Add(new ConfigBlock
                    {
                        Directive = "#",
                        Line = token.Line,
                        Args = new List<string>(),
                        Comment = token.Value[1..].TrimStart()
                    });
                }
                continue;
            }

            // ── End of block ─────────────────────────────────────────────────
            if (token.Value == "}")
            {
                if (depth == 0)
                    RecordError(configFile, result, filename, token.Line,
                                "unexpected \"}\"", options);
                return;
            }

            // ── Directive name ────────────────────────────────────────────────
            var block = new ConfigBlock
            {
                Directive = token.Value,
                Line = token.Line
            };

            // Collect arguments until `;` or `{`
            bool blockOpened = false;
            while (enumerator.MoveNext())
            {
                NgxToken argToken = enumerator.Current;

                if (argToken.Value == ";")
                    break;

                if (argToken.Value == "{")
                {
                    blockOpened = true;
                    break;
                }

                if (argToken.Value == "}" || argToken.Value.StartsWith('#'))
                {
                    // Unexpected end – put the error and stop
                    RecordError(configFile, result, filename, argToken.Line,
                                $"unexpected token \"{argToken.Value}\"", options);
                    return;
                }

                block.Args.Add(argToken.IsQuoted
                    ? $"\"{argToken.Value}\""
                    : argToken.Value);
            }

            if (blockOpened)
            {
                block.Block = new List<ConfigBlock>();
                ParseNestedBlock(enumerator, block.Block, configFile, result, options, filename, depth + 1);
            }

            // ── Handle include directives ─────────────────────────────────────
            if (block.Directive.Equals("include", StringComparison.OrdinalIgnoreCase)
                && options.ParseIncludes
                && block.Args.Count > 0)
            {
                string pattern = block.Args[0].Trim('"', '\'');
                block.Includes = new List<ConfigFile>();
                ResolveIncludes(pattern, filename, block.Includes, result, options);
            }

            configFile.Parsed.Add(block);
        }
    }

    /// <summary>
    /// Parses the contents of a <c>{ … }</c> block into <paramref name="blocks"/>.
    /// </summary>
    private void ParseNestedBlock(
        IEnumerator<NgxToken> enumerator,
        List<ConfigBlock> blocks,
        ConfigFile configFile,
        ParseResult result,
        ParseOptions options,
        string filename,
        int depth)
    {
        while (enumerator.MoveNext())
        {
            NgxToken token = enumerator.Current;

            // ── Comment ───────────────────────────────────────────────────────
            if (token.Value.StartsWith('#'))
            {
                if (options.Comments)
                {
                    blocks.Add(new ConfigBlock
                    {
                        Directive = "#",
                        Line = token.Line,
                        Args = new List<string>(),
                        Comment = token.Value[1..].TrimStart()
                    });
                }
                continue;
            }

            // ── End of block ─────────────────────────────────────────────────
            if (token.Value == "}")
                return;

            // ── Directive name ────────────────────────────────────────────────
            var block = new ConfigBlock
            {
                Directive = token.Value,
                Line = token.Line
            };

            bool blockOpened = false;
            while (enumerator.MoveNext())
            {
                NgxToken argToken = enumerator.Current;

                if (argToken.Value == ";")
                    break;

                if (argToken.Value == "{")
                {
                    blockOpened = true;
                    break;
                }

                if (argToken.Value == "}")
                {
                    RecordError(configFile, result, filename, argToken.Line,
                                "unexpected \"}\"", options);
                    return;
                }

                if (argToken.Value.StartsWith('#'))
                {
                    if (options.Comments)
                    {
                        blocks.Add(new ConfigBlock
                        {
                            Directive = "#",
                            Line = argToken.Line,
                            Args = new List<string>(),
                            Comment = argToken.Value[1..].TrimStart()
                        });
                    }
                    // treat comment as end of current directive (no semicolon)
                    break;
                }

                block.Args.Add(argToken.IsQuoted
                    ? $"\"{argToken.Value}\""
                    : argToken.Value);
            }

            if (blockOpened)
            {
                block.Block = new List<ConfigBlock>();
                ParseNestedBlock(enumerator, block.Block, configFile, result, options, filename, depth + 1);
            }

            // ── Handle include directives ─────────────────────────────────────
            if (block.Directive.Equals("include", StringComparison.OrdinalIgnoreCase)
                && options.ParseIncludes
                && block.Args.Count > 0)
            {
                string pattern = block.Args[0].Trim('"', '\'');
                block.Includes = new List<ConfigFile>();
                ResolveIncludes(pattern, filename, block.Includes, result, options);
            }

            blocks.Add(block);
        }
    }

    private void ResolveIncludes(
        string pattern,
        string currentFile,
        List<ConfigFile> includes,
        ParseResult result,
        ParseOptions options)
    {
        // Resolve relative to the directory of the current file
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(currentFile))
                         ?? Directory.GetCurrentDirectory();

        string fullPattern = Path.IsPathRooted(pattern)
            ? pattern
            : Path.Combine(baseDir, pattern);

        string? dir = Path.GetDirectoryName(fullPattern);
        string filePattern = Path.GetFileName(fullPattern);

        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return;

        foreach (string file in Directory.GetFiles(dir, filePattern).OrderBy(f => f))
        {
            var includedFile = new ConfigFile { File = file };
            try
            {
                IEnumerable<NgxToken> tokens = _lexer.Tokenize(file);
                using var enumerator = tokens.GetEnumerator();
                ParseBlock(enumerator, includedFile, result, options, file, depth: 0);
            }
            catch (Exception ex)
            {
                var error = new ParseError { Error = ex.Message, File = file, Line = 0 };
                includedFile.Errors.Add(error);
                result.Errors.Add(error);
                includedFile.Status = "failed";
            }

            includes.Add(includedFile);
            result.Config.Add(includedFile);
        }
    }

    private void RecordError(
        ConfigFile configFile,
        ParseResult result,
        string filename,
        int line,
        string message,
        ParseOptions options)
    {
        var error = new ParseError { Error = message, File = filename, Line = line };
        configFile.Errors.Add(error);
        result.Errors.Add(error);
        configFile.Status = "failed";
        result.Status = "failed";

        if (!options.CatchErrors)
            throw new NgxParserSyntaxError();
    }
}
