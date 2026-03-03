using System.CommandLine;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class LexCommand
    {
        public static Command Build()
        {
            var cmd = new Command("lex", "Tokenise an NGINX config file to a JSON array.");

            var file      = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
            var outFile   = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");
            var indent    = new Option<int?>(new[] { "-i", "--indent" }, "Number of spaces to indent output.");
            var lineNos   = new Option<bool>(new[] { "-n", "--line-numbers" }, "Include line numbers in JSON.");

            cmd.AddArgument(file);
            cmd.AddOption(outFile);
            cmd.AddOption(indent);
            cmd.AddOption(lineNos);

            cmd.SetHandler(
                (FileInfo f, FileInfo? o, int? ind, bool lineNumbers) =>
                {
                    var tokens = Crossplane.Lex(f.FullName);
                    string json = lineNumbers
                        ? Helpers.SerializeJson(tokens.Select(t => new object[] { t.Value, t.Line }).ToList(), ind ?? -1)
                        : Helpers.SerializeJson(tokens.Select(t => t.Value).ToList(), ind ?? -1);
                    Helpers.WriteOutput(json + "\n", o?.FullName);
                },
                file, outFile, indent, lineNos);

            return cmd;
        }
    }
}

