using System.CommandLine;
using System.Text;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class MinifyCommand
    {
        public static Command Build()
        {
            var cmd = new Command("minify", "Remove all whitespace from an NGINX config.");

            var file    = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
            var outFile = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");

            cmd.AddArgument(file);
            cmd.AddOption(outFile);

            cmd.SetHandler(
                (FileInfo f, FileInfo? o) =>
                {
                    var opts = new ParseOptions
                    {
                        Single      = true,
                        CatchErrors = false,
                        CheckCtx    = false,
                        CheckArgs   = false,
                        Comments    = false,
                        Strict      = false,
                    };
                    ParseResult payload = Crossplane.Parse(f.FullName, opts);
                    Helpers.WriteOutput(BuildMinified(payload.Config[0].Parsed) + "\n", o?.FullName);
                },
                file, outFile);

            return cmd;
        }

        internal static string BuildMinified(List<ConfigBlock> block)
        {
            var sb = new StringBuilder();
            WriteMinifiedBlock(sb, block);
            return sb.ToString();
        }

        private static void WriteMinifiedBlock(StringBuilder sb, List<ConfigBlock> block)
        {
            foreach (var stmt in block)
            {
                sb.Append(Helpers.Enquote(stmt.Directive));

                if (stmt.Directive == "if")
                {
                    sb.Append(" (");
                    sb.Append(string.Join(" ", (stmt.Args ?? new List<string>()).Select(Helpers.Enquote)));
                    sb.Append(")");
                }
                else if (stmt.Args != null && stmt.Args.Count > 0)
                {
                    sb.Append(" ");
                    sb.Append(string.Join(" ", stmt.Args.Select(Helpers.Enquote)));
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
    }
}

