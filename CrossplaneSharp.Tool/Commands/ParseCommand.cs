using System.CommandLine;
using System.CommandLine.Invocation;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class ParseCommand
    {
        public static Command Build()
        {
            var cmd = new Command("parse", "Parse an NGINX config file to a JSON payload.");

            var file     = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
            var outFile  = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");
            var indent   = new Option<int?>(new[] { "-i", "--indent" }, "Number of spaces to indent output.");
            var ignore   = new Option<string>("--ignore", () => "", "Ignore directives (comma-separated).");
            var noCatch  = new Option<bool>("--no-catch", "Stop after the first error.");
            var combine  = new Option<bool>("--combine", "Flatten includes into one single config entry.");
            var single   = new Option<bool>("--single-file", "Do not follow include directives.");
            var comments = new Option<bool>("--include-comments", "Include comments in JSON output.");
            var strict   = new Option<bool>("--strict", "Raise errors for unknown directives.");

            cmd.AddArgument(file);
            cmd.AddOption(outFile);
            cmd.AddOption(indent);
            cmd.AddOption(ignore);
            cmd.AddOption(noCatch);
            cmd.AddOption(combine);
            cmd.AddOption(single);
            cmd.AddOption(comments);
            cmd.AddOption(strict);

            cmd.SetHandler((InvocationContext ctx) =>
            {
                var f        = ctx.ParseResult.GetValueForArgument(file);
                var o        = ctx.ParseResult.GetValueForOption(outFile);
                var ind      = ctx.ParseResult.GetValueForOption(indent);
                var ign      = ctx.ParseResult.GetValueForOption(ignore) ?? "";
                var noCatchV = ctx.ParseResult.GetValueForOption(noCatch);
                var combineV = ctx.ParseResult.GetValueForOption(combine);
                var singleV  = ctx.ParseResult.GetValueForOption(single);
                var commentsV= ctx.ParseResult.GetValueForOption(comments);
                var strictV  = ctx.ParseResult.GetValueForOption(strict);

                var options = new ParseOptions
                {
                    CatchErrors = !noCatchV,
                    Ignore      = new HashSet<string>(
                                      ign.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries),
                                      System.StringComparer.Ordinal),
                    Single   = singleV,
                    Comments = commentsV,
                    Strict   = strictV,
                    Combine  = combineV,
                };

                ParseResult result = Crossplane.Parse(f.FullName, options);
                Helpers.WriteOutput(Helpers.SerializeJson(result, ind ?? -1) + "\n", o?.FullName);
            });

            return cmd;
        }
    }
}

