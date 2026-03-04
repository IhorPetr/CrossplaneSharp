using System.CommandLine;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class LexCommand
    {
        public static Command Build()
        {
            var cmd = new Command("lex", "Tokenise an NGINX config file to a JSON array.");

            var file    = new Argument<FileInfo>("filename") { Description = "The NGINX config file.", Arity = ArgumentArity.ExactlyOne };
            var outFile = new Option<FileInfo?>("-o", ["--out"]) { Description = "Write output to a file." };
            var indent  = new Option<int?>("-i", ["--indent"]) { Description = "Number of spaces to indent output." };
            var lineNos = new Option<bool>("-n", ["--line-numbers"]) { Description = "Include line numbers in JSON." };

            cmd.Add(file);
            cmd.Add(outFile);
            cmd.Add(indent);
            cmd.Add(lineNos);

            cmd.SetAction(ctx =>
            {
                var f           = ctx.GetRequiredValue(file);
                var o           = ctx.GetValue(outFile);
                var ind         = ctx.GetValue(indent);
                var lineNumbers = ctx.GetValue(lineNos);

                var tokens = Crossplane.Lex(f.FullName);
                string json = lineNumbers
                    ? Helpers.SerializeJson(tokens.Select(t => new object[] { t.Value, t.Line }).ToList(), ind ?? -1)
                    : Helpers.SerializeJson(tokens.Select(t => t.Value).ToList(), ind ?? -1);
                Helpers.WriteOutput(json + "\n", o?.FullName);
            });

            return cmd;
        }
    }
}
