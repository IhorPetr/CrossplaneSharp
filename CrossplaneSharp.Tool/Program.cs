using System.CommandLine;
using CrossplaneSharp.Tool.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// crossplane-sharp  –  CLI tool for working with NGINX configuration files
// ─────────────────────────────────────────────────────────────────────────────

var rootCommand = new RootCommand("Various operations for NGINX config files.")
{
    Name = "crossplane-sharp"
};

rootCommand.AddCommand(ParseCommand.Build());
rootCommand.AddCommand(BuildCommand.Build());
rootCommand.AddCommand(LexCommand.Build());
rootCommand.AddCommand(MinifyCommand.Build());
rootCommand.AddCommand(FormatCommand.Build());

return await rootCommand.InvokeAsync(args);
