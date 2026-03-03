using System.CommandLine;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class FormatCommand
    {
        public static Command Build()
        {
            var cmd = new Command("format", "Format an NGINX config file.");

            var file    = new Argument<FileInfo>("filename", "The NGINX config file.") { Arity = ArgumentArity.ExactlyOne };
            var outFile = new Option<FileInfo?>(new[] { "-o", "--out" }, "Write output to a file.");
            var indent  = new Option<int>(new[] { "-i", "--indent" }, () => 4, "Spaces per indent level.");
            var tabs    = new Option<bool>(new[] { "-t", "--tabs" }, "Indent with tabs instead of spaces.");

            cmd.AddArgument(file);
            cmd.AddOption(outFile);
            cmd.AddOption(indent);
            cmd.AddOption(tabs);

            cmd.SetHandler(
                (FileInfo f, FileInfo? o, int ind, bool t) =>
                {
                    ParseResult payload = Crossplane.Parse(f.FullName, new ParseOptions
                    {
                        Comments  = true,
                        Single    = true,
                        CheckCtx  = false,
                        CheckArgs = false,
                    });

                    if (payload.Status != "ok")
                    {
                        var e = payload.Errors[0];
                        Console.Error.WriteLine(
                            $"crossplane-sharp: error: {e.File ?? f.FullName}:{e.Line}: {e.Error}");
                        Environment.Exit(1);
                        return;
                    }

                    string output = Crossplane.Build(payload.Config[0].Parsed,
                        new BuildOptions { Indent = ind, Tabs = t, Header = false });
                    Helpers.WriteOutput(output + "\n", o?.FullName);
                },
                file, outFile, indent, tabs);

            return cmd;
        }
    }
}

