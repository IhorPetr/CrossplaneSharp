using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossplaneSharp.Exceptions;

namespace CrossplaneSharp
{

    /// <summary>
    /// Parses one or more NGINX configuration files into a <see cref="ParseResult"/>.
    /// C# port of Python crossplane <c>parser.py</c> — including context tracking,
    /// include-file index management, <c>if(…)</c> arg stripping, and combine mode.
    /// </summary>
    public class NginxParser
    {
        private readonly NginxLexer _lexer = new NginxLexer();

        // ──────────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses the NGINX config at <paramref name="filename"/>.
        /// Mirrors Python <c>crossplane.parse(filename, …)</c>.
        /// </summary>
        public ParseResult Parse(string filename, ParseOptions options = null)
        {
            options = options ?? new ParseOptions();
            filename = PathHelper.GetFullPath(filename);
            string configDir = Path.GetDirectoryName(filename) ?? Directory.GetCurrentDirectory();

            var payload = new ParseResult();

            // Work-list of (filename, context) to parse – grows as includes are found
            var includes = new List<(string File, IReadOnlyList<string> Ctx)>
                { (filename, Array.Empty<string>()) };
            // Use OS-appropriate comparer: case-insensitive on Windows, ordinal on Unix
            var included = new Dictionary<string, int>(PathHelper.PathComparer)
                { [filename] = 0 };

            // iterate – list grows during iteration
            for (int i = 0; i < includes.Count; i++)
            {
                var (fname, ctx) = includes[i];
                var parsing = new ConfigFile { File = fname };

                IEnumerable<NgxToken> tokens;
                try
                {
                    tokens = _lexer.Tokenize(fname).ToList();   // materialise so balance-brace runs eagerly
                }
                catch (Exception ex)
                {
                    HandleError(parsing, payload, fname, ex, options);
                    payload.Config.Add(parsing);
                    continue;
                }

                try
                {
                    parsing.Parsed = ParseBlock(
                        parsing, payload, (List<(string, IReadOnlyList<string>)>)includes,
                        included, configDir, options,
                        tokens.GetEnumerator(), ctx);
                }
                catch (Exception ex)
                {
                    if (!options.CatchErrors) throw;
                    HandleError(parsing, payload, fname, ex, options);
                }

                payload.Config.Add(parsing);
            }

            return options.Combine ? CombineConfigs(payload) : payload;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Core recursive parser
        // ──────────────────────────────────────────────────────────────────────────

        private List<ConfigBlock> ParseBlock(
            ConfigFile parsing,
            ParseResult payload,
            List<(string File, IReadOnlyList<string> Ctx)> includes,
            Dictionary<string, int> included,
            string configDir,
            ParseOptions options,
            IEnumerator<NgxToken> tokens,
            IReadOnlyList<string> ctx,
            bool consume = false)
        {
            var parsed = new List<ConfigBlock>();

            while (tokens.MoveNext())
            {
                var (tok, lineno, quoted) = (tokens.Current.Value, tokens.Current.Line, tokens.Current.IsQuoted);
                var commentsInArgs = new List<string>();

                // closing brace → end of block
                if (tok == "}" && !quoted) break;

                // consume mode: swallow everything, descend into nested blocks
                if (consume)
                {
                    if (tok == "{" && !quoted)
                        ParseBlock(parsing, payload, includes, included, configDir, options,
                            tokens, ctx, consume: true);
                    continue;
                }

                string directive = tok;
                var stmt = new ConfigBlock
                {
                    Directive = directive,
                    Line = lineno,
                    Args = new List<string>()
                };
                if (options.Combine)
                    stmt.File = parsing.File;

                // comment token
                if (directive.StartsWith("#") && !quoted)
                {
                    if (options.Comments)
                    {
                        stmt.Directive = "#";
                        stmt.Comment = tok.Substring(1);
                        parsed.Add(stmt);
                    }
                    continue;
                }

                // collect arguments until ; or { or }
                string term = ";";
                while (tokens.MoveNext())
                {
                    var arg = tokens.Current;
                    if (!arg.IsQuoted && (arg.Value == ";" || arg.Value == "{" || arg.Value == "}"))
                    {
                        term = arg.Value;
                        break;
                    }
                    if (arg.Value.StartsWith("#") && !arg.IsQuoted)
                        commentsInArgs.Add(arg.Value.Substring(1));
                    else
                        stmt.Args.Add(arg.Value);
                }

                // skip ignored directives
                if (options.Ignore.Contains(directive))
                {
                    if (term == "{")
                        ParseBlock(parsing, payload, includes, included, configDir, options,
                            tokens, ctx, consume: true);
                    continue;
                }

                // strip parens from "if (…)" args
                if (directive == "if")
                    PrepareIfArgs(stmt.Args);

                // validate
                try
                {
                    NginxAnalyzer.Analyze(
                        parsing.File, directive, lineno, stmt.Args, term, ctx,
                        options.Strict, options.CheckCtx, options.CheckArgs);
                }
                catch (NgxParserDirectiveError ex)
                {
                    if (options.CatchErrors)
                    {
                        HandleError(parsing, payload, parsing.File, ex, options);
                        if (ex.Strerror.EndsWith(" is not terminated by \";\""))
                        {
                            if (term != "}" && !quoted)
                                ParseBlock(parsing, payload, includes, included, configDir, options,
                                    tokens, ctx, consume: true);
                            else break;
                        }
                        continue;
                    }
                    throw;
                }

                // handle include directives
                if (!options.Single && directive == "include" && stmt.Args.Count > 0)
                {
                    stmt.Includes = new List<int>();
                    string pattern = stmt.Args[0];
                    // Normalise separator: NGINX configs use '/', convert to OS-native
                    if (!PathHelper.IsPathRooted(pattern))
                        pattern = PathHelper.Combine(configDir, pattern);
                    else
                        pattern = PathHelper.ToNative(pattern);

                    List<string> fnames;
                    if (HasGlobMagic(pattern))
                    {
                        string dir2 = Path.GetDirectoryName(pattern) ?? ".";
                        string fileGlob = Path.GetFileName(pattern);
                        fnames = Directory.Exists(dir2)
                            ? Directory.GetFiles(dir2, fileGlob).OrderBy(f => f).ToList()
                            : new List<string>();
                    }
                    else
                    {
                        try { File.OpenRead(pattern).Dispose(); fnames = new List<string> { pattern }; }
                        catch (Exception ex)
                        {
                            fnames = new List<string>();
                            var wrapped = new IOException(ex.Message);
                            if (options.CatchErrors)
                                HandleError(parsing, payload, parsing.File, wrapped, options);
                            else throw;
                        }
                    }

                    foreach (var incFile in fnames)
                    {
                        string absInc = PathHelper.GetFullPath(incFile);
                        if (!included.ContainsKey(absInc))
                        {
                            included[absInc] = includes.Count;
                            includes.Add((absInc, ctx));
                        }
                        stmt.Includes.Add(included[absInc]);
                    }
                }

                // recurse into block
                if (term == "{")
                {
                    var inner = NginxAnalyzer.EnterBlockCtx(directive, ctx);
                    stmt.Block = ParseBlock(parsing, payload, includes, included,
                        configDir, options, tokens, inner);
                }

                parsed.Add(stmt);

                // emit any inline comments that were inside the arg list
                foreach (var c in commentsInArgs)
                    parsed.Add(new ConfigBlock { Directive = "#", Line = lineno, Args = new List<string>(), Comment = c });
            }

            return parsed;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Combine mode
        // ──────────────────────────────────────────────────────────────────────────

        private static ParseResult CombineConfigs(ParseResult old)
        {
            var oldConfigs = old.Config;

            IEnumerable<ConfigBlock> PerformIncludes(IEnumerable<ConfigBlock> block)
            {
                foreach (var stmt in block)
                {
                    if (stmt.Block != null)
                        stmt.Block = PerformIncludes(stmt.Block).ToList();

                    if (stmt.Includes != null)
                    {
                        foreach (var idx in stmt.Includes)
                        {
                            var config = oldConfigs[idx].Parsed;
                            foreach (var s in PerformIncludes(config))
                                yield return s;
                        }
                    }
                    else
                    {
                        yield return stmt;
                    }
                }
            }

            var combined = new ConfigFile
            {
                File = oldConfigs[0].File,
                Status = "ok",
                Errors = oldConfigs.SelectMany(c => c.Errors).ToList(),
                Parsed = PerformIncludes(oldConfigs[0].Parsed).ToList()
            };

            if (oldConfigs.Any(c => c.Status == "failed"))
                combined.Status = "failed";

            return new ParseResult
            {
                Status = old.Status,
                Errors = old.Errors,
                Config = new List<ConfigFile> { combined }
            };
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private static void HandleError(
            ConfigFile parsing, ParseResult payload,
            string fname, Exception ex, ParseOptions options)
        {
            int? line = (ex as NgxParserBaseException)?.Lineno;
            var err = new ParseError
            {
                Error = ex.Message,
                File = fname,
                Line = line,
                Callback = options.OnError?.Invoke(ex)
            };
            parsing.Errors.Add(new ParseError { Error = ex.Message, Line = line });
            parsing.Status = "failed";
            payload.Errors.Add(err);
            payload.Status = "failed";
        }

        /// <summary>Strips outer parentheses from <c>if (…)</c> args. Mirrors Python _prepare_if_args.</summary>
        private static void PrepareIfArgs(List<string> args)
        {
            if (args.Count == 0) return;
            int last = args.Count - 1;
            if (args[0].StartsWith("(") && args[last].EndsWith(")"))
            {
                args[0]    = args[0].Substring(1).TrimStart();
                args[last] = args[last].Substring(0, args[last].Length - 1).TrimEnd();
                int start = args[0].Length == 0 ? 1 : 0;
                int end   = args[last].Length == 0 ? args.Count - 1 : args.Count;
                var trimmed = args.GetRange(start, end - start);
                args.Clear();
                args.AddRange(trimmed);
            }
        }

        private static bool HasGlobMagic(string pattern) =>
            pattern.IndexOf('*') >= 0 || pattern.IndexOf('?') >= 0 || pattern.IndexOf('[') >= 0;
    }
}