using System.CommandLine;
using CrossplaneSharp.Tool.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// crossplane-sharp  –  CLI tool for working with NGINX configuration files
// ─────────────────────────────────────────────────────────────────────────────

var rootCommand = new RootCommand("Various operations for NGINX config files.");

rootCommand.Add(ParseCommand.Build());
rootCommand.Add(BuildCommand.Build());
rootCommand.Add(LexCommand.Build());
rootCommand.Add(MinifyCommand.Build());
rootCommand.Add(FormatCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
