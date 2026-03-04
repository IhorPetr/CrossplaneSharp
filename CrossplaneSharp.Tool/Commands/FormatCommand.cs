using System.CommandLine;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class FormatCommand
    {
        public static Command Build()
        {
            var cmd = new Command("format", "Format an NGINX config file.");

            var file    = new Argument<FileInfo>("filename") { Description = "The NGINX config file.", Arity = ArgumentArity.ExactlyOne };
            var outFile = new Option<FileInfo?>("-o", ["--out"]) { Description = "Write output to a file." };
            var indent  = new Option<int>("-i", ["--indent"]) { Description = "Spaces per indent level.", DefaultValueFactory = _ => 4 };
            var tabs    = new Option<bool>("-t", ["--tabs"]) { Description = "Indent with tabs instead of spaces." };

            cmd.Add(file);
            cmd.Add(outFile);
            cmd.Add(indent);
            cmd.Add(tabs);

            cmd.SetAction(ctx =>
            {
                var f    = ctx.GetRequiredValue(file);
                var o   = ctx.GetValue(outFile);
                var ind      = ctx.GetValue(indent);
                var t      = ctx.GetValue(tabs);

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
                        $"crossplanesharp: error: {e.File ?? f.FullName}:{e.Line}: {e.Error}");
                    Environment.Exit(1);
                    return;
                }

                string output = Crossplane.Build(payload.Config[0].Parsed,
                    new BuildOptions { Indent = ind, Tabs = t, Header = false });
                Helpers.WriteOutput(output + "\n", o?.FullName);
            });

            return cmd;
        }
    }
}
