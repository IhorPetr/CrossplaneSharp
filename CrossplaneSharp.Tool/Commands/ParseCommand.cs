using System.CommandLine;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class ParseCommand
    {
        public static Command Build()
        {
            var cmd = new Command("parse", "Parse an NGINX config file to a JSON payload.");

            var file     = new Argument<FileInfo>("filename") { Description = "The NGINX config file.", Arity = ArgumentArity.ExactlyOne };
            var outFile  = new Option<FileInfo?>("-o", ["--out" ]) { Description = "Write output to a file." };
            var indent   = new Option<int?>("-i", ["--indent"]) { Description = "Number of spaces to indent output." };
            var ignore   = new Option<string>("--ignore") { Description = "Ignore directives (comma-separated).", DefaultValueFactory = _ => "" };
            var noCatch  = new Option<bool>("--no-catch") { Description = "Stop after the first error." };
            var combine  = new Option<bool>("--combine") { Description = "Flatten includes into one single config entry." };
            var single   = new Option<bool>("--single-file") { Description = "Do not follow include directives." };
            var comments = new Option<bool>("--include-comments") { Description = "Include comments in JSON output." };
            var strict   = new Option<bool>("--strict") { Description = "Raise errors for unknown directives." };

            cmd.Add(file);
            cmd.Add(outFile);
            cmd.Add(indent);
            cmd.Add(ignore);
            cmd.Add(noCatch);
            cmd.Add(combine);
            cmd.Add(single);
            cmd.Add(comments);
            cmd.Add(strict);

            cmd.SetAction(ctx =>
            {
                var f         = ctx.GetRequiredValue(file);
                var o         = ctx.GetValue(outFile);
                var ind       = ctx.GetValue(indent);
                var ign       = ctx.GetValue(ignore) ?? "";
                var noCatchV  = ctx.GetValue(noCatch);
                var combineV  = ctx.GetValue(combine);
                var singleV   = ctx.GetValue(single);
                var commentsV = ctx.GetValue(comments);
                var strictV   = ctx.GetValue(strict);

                var options = new ParseOptions
                {
                    CatchErrors = !noCatchV,
                    Ignore      = new HashSet<string>(
                                      ign.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries),
                                      StringComparer.Ordinal),
                    Single      = singleV,
                    Comments    = commentsV,
                    Strict      = strictV,
                    Combine     = combineV,
                };

                ParseResult result = Crossplane.Parse(f.FullName, options);
                Helpers.WriteOutput(Helpers.SerializeJson(result, ind ?? -1) + "\n", o?.FullName);
            });

            return cmd;
        }
    }
}

