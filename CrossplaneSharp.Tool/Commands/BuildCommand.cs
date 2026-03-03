using System.CommandLine;
using System.CommandLine.Invocation;

namespace CrossplaneSharp.Tool.Commands
{
    internal static class BuildCommand
    {
        public static Command Build()
        {
            var cmd = new Command("build", "Build NGINX config files from a JSON payload.");

            var file      = new Argument<FileInfo>("filename", "The JSON payload file.") { Arity = ArgumentArity.ExactlyOne };
            var verbose   = new Option<bool>(new[] { "-v", "--verbose" }, "Print paths of written files.");
            var dir       = new Option<DirectoryInfo?>(new[] { "-d", "--dir" }, "Base directory to build in.");
            var force     = new Option<bool>(new[] { "-f", "--force" }, "Overwrite existing files without prompting.");
            var indent    = new Option<int>(new[] { "-i", "--indent" }, () => 4, "Spaces per indent level.");
            var tabs      = new Option<bool>(new[] { "-t", "--tabs" }, "Indent with tabs instead of spaces.");
            var noHeaders = new Option<bool>("--no-headers", "Do not write the header comment to configs.");
            var stdout    = new Option<bool>("--stdout", "Write configs to stdout instead of files.");

            cmd.AddArgument(file);
            cmd.AddOption(verbose);
            cmd.AddOption(dir);
            cmd.AddOption(force);
            cmd.AddOption(indent);
            cmd.AddOption(tabs);
            cmd.AddOption(noHeaders);
            cmd.AddOption(stdout);

            cmd.SetHandler((InvocationContext ctx) =>
            {
                var f         = ctx.ParseResult.GetValueForArgument(file);
                var verboseV  = ctx.ParseResult.GetValueForOption(verbose);
                var dirV      = ctx.ParseResult.GetValueForOption(dir);
                var forceV    = ctx.ParseResult.GetValueForOption(force);
                var indentV   = ctx.ParseResult.GetValueForOption(indent);
                var tabsV     = ctx.ParseResult.GetValueForOption(tabs);
                var noHdrV    = ctx.ParseResult.GetValueForOption(noHeaders);
                var stdoutV   = ctx.ParseResult.GetValueForOption(stdout);

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
                        System.Console.Write("# " + path + "\n" + output + "\n");
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
                        System.Console.WriteLine($"building '{f.FullName}' would overwrite these files:");
                        System.Console.WriteLine(string.Join("\n", existing));
                        System.Console.Write("overwrite? (y/n [n]) ");
                        string? answer = System.Console.ReadLine();
                        if (answer == null || !answer.Trim().ToLowerInvariant().StartsWith("y"))
                        {
                            System.Console.WriteLine("not overwritten");
                            return;
                        }
                    }
                }

                Crossplane.BuildFiles(payload, baseDir, opts);

                if (verboseV)
                    foreach (var config in payload.Config)
                        System.Console.WriteLine("wrote to " + Helpers.ResolvePath(config.File, baseDir));
            });

            return cmd;
        }
    }
}

