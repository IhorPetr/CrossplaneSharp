using System.CommandLine;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class BuildCommand
    {
        public static Command Build()
        {
            var cmd = new Command("build", "Build NGINX config files from a JSON payload.");

            var file      = new Argument<FileInfo>("filename") { Description = "The JSON payload file.", Arity = ArgumentArity.ExactlyOne };
            var verbose   = new Option<bool>("-v", ["--verbose"]) { Description = "Print paths of written files." };
            var dir       = new Option<DirectoryInfo?>("-d", ["--dir"]) { Description = "Base directory to build in." };
            var force     = new Option<bool>("-f", ["--force"]) { Description = "Overwrite existing files without prompting." };
            var indent    = new Option<int>("-i", ["--indent"]) { Description = "Spaces per indent level.", DefaultValueFactory = _ => 4 };
            var tabs      = new Option<bool>("-t", ["--tabs"]) { Description = "Indent with tabs instead of spaces." };
            var noHeaders = new Option<bool>("--no-headers") { Description = "Do not write the header comment to configs." };
            var stdout    = new Option<bool>("--stdout") { Description = "Write configs to stdout instead of files." };

            cmd.Add(file);
            cmd.Add(verbose);
            cmd.Add(dir);
            cmd.Add(force);
            cmd.Add(indent);
            cmd.Add(tabs);
            cmd.Add(noHeaders);
            cmd.Add(stdout);

            cmd.SetAction(ctx =>
            {
                var f         = ctx.GetRequiredValue(file);
                var verboseV    = ctx.GetValue(verbose);
                var dirV = ctx.GetValue(dir);
                var forceV      = ctx.GetValue(force);
                var indentV       = ctx.GetValue(indent);
                var tabsV       = ctx.GetValue(tabs);
                var noHdrV      = ctx.GetValue(noHeaders);
                var stdoutV     = ctx.GetValue(stdout);

                string json = File.ReadAllText(f.FullName);
                ParseResult payload = Helpers.DeserializeParseResult(json);
                string baseDir = dirV?.FullName ?? Directory.GetCurrentDirectory();
                var opts = new BuildOptions { Indent = indentV, Tabs = tabsV, Header = !noHdrV };

                if (stdoutV)
                {
                    foreach (var config in payload.Config)
                    {
                        string path   = Helpers.ResolvePath(config.File, baseDir);
                        string output = Crossplane.Build(config.Parsed, opts).TrimEnd() + "\n";
                        Console.Write("# " + path + "\n" + output + "\n");
                    }
                    return;
                }

                if (!forceV)
                {
                    var existing = payload.Config
                        .Select(c => Helpers.ResolvePath(c.File, baseDir))
                        .Where(File.Exists)
                        .ToList();

                    if (existing.Count > 0)
                    {
                        Console.WriteLine($"building '{f.FullName}' would overwrite these files:");
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

                if (verboseV)
                    foreach (var config in payload.Config)
                        Console.WriteLine("wrote to " + Helpers.ResolvePath(config.File, baseDir));
            });

            return cmd;
        }
    }
}
