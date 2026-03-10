using System.Runtime.InteropServices;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class BuilderTests
{
    private static readonly string NginxDir =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx");

    private static string Build(List<ConfigBlock> blocks, BuildOptions? opts = null) =>
        Crossplane.Build(blocks, opts);

    private static List<ConfigBlock> ParseBlocks(string fixtureSub) =>
        Crossplane.Parse(Path.Combine(NginxDir, fixtureSub), new ParseOptions { Single = true })
            .Config[0].Parsed;

    // ── basic output ───────────────────────────────────────────────────────

    [Test]
    public void SimpleDirective_SemicolonTerminated()
    {
        var out_ = Crossplane.Build([new() { Directive = "daemon", Args = ["off"] }]);
        Assert.That(out_.Trim(), Is.EqualTo("daemon off;"));
    }

    [Test]
    public void NoArgs_JustSemicolon()
    {
        var out_ = Crossplane.Build([new() { Directive = "gzip_vary" }]);
        Assert.That(out_.Trim(), Is.EqualTo("gzip_vary;"));
    }

    [Test]
    public void MultipleArgs_SpaceSeparated()
    {
        var out_ = Crossplane.Build([new() { Directive = "error_page", Args = ["404", "/404.html"] }]);
        Assert.That(out_.Trim(), Is.EqualTo("error_page 404 /404.html;"));
    }

    [Test]
    public void BlockDirective_BracesFormatted()
    {
        var out_ = Crossplane.Build([new()
        {
            Directive = "events",
            Block = [new() { Directive = "worker_connections", Args = ["1024"] }]
        }]);
        Assert.That(out_, Does.Contain("events {"));
        Assert.That(out_, Does.Contain("worker_connections 1024;"));
        Assert.That(out_, Does.Contain("}"));
    }

    [Test]
    public void EmptyBlock_OpenAndClose()
    {
        var out_ = Crossplane.Build([new() { Directive = "events", Block = [] }]);
        Assert.That(out_, Does.Contain("events {"));
        Assert.That(out_, Does.Contain("}"));
    }

    // ── indentation ────────────────────────────────────────────────────────

    [Test]
    public void DefaultIndent_FourSpaces()
    {
        var out_ = Crossplane.Build([new()
        {
            Directive = "events",
            Block = [new() { Directive = "worker_connections", Args = ["1024"] }]
        }]);
        Assert.That(out_, Does.Contain("    worker_connections 1024;"));
    }

    [Test]
    public void CustomIndent_TwoSpaces()
    {
        var out_ = Build([new()
        {
            Directive = "events",
            Block = [new() { Directive = "worker_connections", Args = ["1024"] }]
        }], new BuildOptions { Indent = 2 });
        Assert.That(out_, Does.Contain("  worker_connections 1024;"));
    }

    [Test]
    public void TabIndent_UsesTabs()
    {
        var out_ = Build([new()
        {
            Directive = "events",
            Block = [new() { Directive = "worker_connections", Args = ["1024"] }]
        }], new BuildOptions { Tabs = true });
        Assert.That(out_, Does.Contain("\tworker_connections 1024;"));
    }

    // ── header ─────────────────────────────────────────────────────────────

    [Test]
    public void Header_ContainsCrossplaneComment()
    {
        var out_ = Build([], new BuildOptions { Header = true });
        Assert.That(out_, Does.Contain("# This config was built from JSON using NGINX crossplane."));
    }

    [Test]
    public void NoHeader_NoHeaderComment()
    {
        var out_ = Build([new() { Directive = "daemon", Args = ["off"] }],
            new BuildOptions { Header = false });
        Assert.That(out_, Does.Not.Contain("This config was built"));
    }

    // ── if directive ───────────────────────────────────────────────────────

    [Test]
    public void IfDirective_ArgsWrappedInParens()
    {
        var out_ = Build([new()
        {
            Directive = "if",
            Args = ["$request_method", "=", "POST"],
            Block = [new() { Directive = "return", Args = ["405"] }]
        }]);
        Assert.That(out_, Does.Contain("if ($request_method = POST)"));
    }

    // ── comments ───────────────────────────────────────────────────────────

    [Test]
    public void StandaloneComment_HasSpaceAfterHash()
    {
        var out_ = Build([new() { Directive = "#", Comment = "my comment" }]);
        Assert.That(out_, Does.Contain("# my comment"));
    }

    [Test]
    public void EmptyComment_JustHash()
    {
        var out_ = Build([new() { Directive = "#", Comment = "" }]);
        Assert.That(out_.Trim(), Is.EqualTo("#"));
    }

    [Test]
    public void InlineComment_SameLine_Attached()
    {
        var out_ = Build([
            new() { Directive = "worker_processes", Args = ["4"], Line = 5 },
            new() { Directive = "#", Comment = "inline", Line = 5 }
        ]);
        var line = out_.Split('\n').First(l => l.Contains("worker_processes"));
        Assert.That(line, Does.Contain("# inline"));
    }

    [Test]
    public void Comment_DifferentLine_NewLine()
    {
        var out_ = Build([
            new() { Directive = "worker_processes", Args = ["4"], Line = 5 },
            new() { Directive = "#", Comment = "other", Line = 6 }
        ]);
        var lines = out_.Split('\n');
        Assert.That(lines.Any(l => l.Trim().StartsWith("# other")), Is.True);
    }

    // ── enquoting ──────────────────────────────────────────────────────────

    [Test]
    public void EmptyArg_Quoted()
    {
        var out_ = Build([new() { Directive = "set", Args = ["$v", ""] }]);
        Assert.That(out_, Does.Contain("\"\""));
    }

    [Test]
    public void ArgWithSpace_Quoted()
    {
        var out_ = Build([new() { Directive = "add_header", Args = ["Cache-Control", "no-cache, no-store"] }]);
        Assert.That(out_, Does.Contain("\"no-cache, no-store\""));
    }

    [Test]
    public void PlainArg_NotQuoted()
    {
        var out_ = Build([new() { Directive = "root", Args = ["/var/www/html"] }]);
        Assert.That(out_.Trim(), Is.EqualTo("root /var/www/html;"));
    }

    // ── round-trips ────────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_Simple_ContainsExpectedContent()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("events {"));
        Assert.That(out_, Does.Contain("worker_connections 1024;"));
        Assert.That(out_, Does.Contain("http {"));
        Assert.That(out_, Does.Contain("server {"));
        Assert.That(out_, Does.Contain("listen 127.0.0.1:8080;"));
        Assert.That(out_, Does.Contain("location / {"));
        Assert.That(out_, Does.Contain("return 200"));
    }

    [Test]
    public void RoundTrip_Simple_IsSemicolonTerminated()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        foreach (var line in out_.Split('\n').Select(l => l.Trim())
                                  .Where(l => l.Length > 0 && !l.StartsWith("#") && l != "}"))
        {
            Assert.That(line.EndsWith(";") || line.EndsWith("{"), Is.True,
                $"Line not properly terminated: '{line}'");
        }
    }

    [Test]
    public void RoundTrip_WithComments_CommentsPreserved()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "with-comments\\nginx.conf" : "with-comments/nginx.conf";
        var blocks = Crossplane.Parse(Path.Combine(NginxDir, filePath),
                   new ParseOptions { Single = true, Comments = true })
            .Config[0].Parsed;
        var out_ = Build(blocks);
        Assert.That(out_, Does.Contain("#"));
    }

    [Test]
    public void RoundTrip_DirectiveWithSpace_MapPreserved()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "directive-with-space\\nginx.conf" : "directive-with-space/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("map $http_user_agent $mobile {"));
    }

    [Test]
    public void RoundTrip_EmptyValueMap_EmptyStringReQuoted()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "empty-value-map\\nginx.conf" : "empty-value-map/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("\"\""));
    }

    [Test]
    public void RoundTrip_QuotedRightBrace_ClosingBraceInsideQuotes()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "quoted-right-brace\\nginx.conf" : "quoted-right-brace/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("log_format"));
        Assert.That(out_, Does.Contain("@timestamp"));
    }

    [Test]
    public void RoundTrip_RussianText_EnvDirectivePresent()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "russian-text\\nginx.conf" : "russian-text/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("env "));
        Assert.That(out_, Does.Contain("events {"));
    }

    // ── NginxBuilder direct API ────────────────────────────────────────────

    [Test]
    public void NginxBuilder_Build_SimpleDirective()
    {
        
        var result = Crossplane.Build(new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "worker_processes", Args = new List<string> { "2" } }
        });
        Assert.That(result.Trim(), Is.EqualTo("worker_processes 2;"));
    }

    [Test]
    public void NginxBuilder_Build_WithHeader()
    {
        
        var result = Crossplane.Build(
            new List<ConfigBlock> { new ConfigBlock { Directive = "gzip_vary" } },
            new BuildOptions { Header = true });
        Assert.That(result, Does.Contain("# This config was built"));
    }

    [Test]
    public void NginxBuilder_Build_WithTabs()
    {
        
        var result = Crossplane.Build(new List<ConfigBlock>
        {
            new ConfigBlock
            {
                Directive = "events",
                Block = new List<ConfigBlock>
                {
                    new ConfigBlock { Directive = "worker_connections", Args = new List<string> { "1024" } }
                }
            }
        }, new BuildOptions { Tabs = true });
        Assert.That(result, Does.Contain("\tworker_connections 1024;"));
    }

    [Test]
    public void NginxBuilder_Build_EnquotesArgWithSpace()
    {
        
        var result = Crossplane.Build(new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "add_header", Args = new List<string> { "Cache-Control", "no-store, no-cache" } }
        });
        Assert.That(result, Does.Contain("\"no-store, no-cache\""));
    }

    [Test]
    public void NginxBuilder_Build_EnquotesEmptyArg()
    {
        
        var result = Crossplane.Build(new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "set", Args = new List<string> { "$v", "" } }
        });
        Assert.That(result, Does.Contain("\"\""));
    }

    [Test]
    public void NginxBuilder_Build_IfDirectiveWrapsInParens()
    {
        
        var result = Crossplane.Build(new List<ConfigBlock>
        {
            new ConfigBlock
            {
                Directive = "if",
                Args = new List<string> { "$request_method", "=", "POST" },
                Block = new List<ConfigBlock>
                {
                    new ConfigBlock { Directive = "return", Args = new List<string> { "405" } }
                }
            }
        });
        Assert.That(result, Does.Contain("if ($request_method = POST)"));
    }

    [Test]
    public void NginxBuilder_BuildFiles_CreatesSubDirectory()
    {
        
        var dir = Path.Combine(Path.GetTempPath(), $"ngx_{Guid.NewGuid():N}");
        try
        {
            var outPath = Path.Combine(dir, "sub", "nginx.conf");
            var payload = new ParseResult
            {
                Status = "ok",
                Config = new List<ConfigFile>
                {
                    new ConfigFile
                    {
                        File   = outPath,
                        Status = "ok",
                        Parsed = new List<ConfigBlock>
                        {
                            new ConfigBlock { Directive = "worker_processes", Args = new List<string> { "1" } }
                        }
                    }
                }
            };
            Crossplane.BuildFiles(payload);
            Assert.That(File.Exists(outPath), Is.True);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    // ── BuildOptions default constructor ──────────────────────────────────

    [Test]
    public void BuildOptions_Defaults_AreCorrect()
    {
        var opts = new BuildOptions();
        Assert.That(opts.Indent, Is.EqualTo(4));
        Assert.That(opts.Tabs,   Is.False);
        Assert.That(opts.Header, Is.False);
    }

    // ── Crossplane.Build / BuildFiles entry-points ────────────────────────

    [Test]
    public void Crossplane_Build_ReturnsConfigString()
    {
        var blocks = new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "worker_processes", Args = new List<string> { "4" } }
        };
        var result = Crossplane.Build(blocks);
        Assert.That(result.Trim(), Is.EqualTo("worker_processes 4;"));
    }

    [Test]
    public void Crossplane_Build_WithOptions_RespectsIndent()
    {
        var blocks = new List<ConfigBlock>
        {
            new ConfigBlock
            {
                Directive = "events",
                Block = new List<ConfigBlock>
                {
                    new ConfigBlock { Directive = "worker_connections", Args = new List<string> { "512" } }
                }
            }
        };
        var result = Crossplane.Build(blocks, new BuildOptions { Indent = 2 });
        Assert.That(result, Does.Contain("  worker_connections 512;"));
    }

    [Test]
    public void Crossplane_BuildFiles_WritesFileToDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ngx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var outPath = Path.Combine(dir, "out.conf");
            var payload = new ParseResult
            {
                Status = "ok",
                Config = new List<ConfigFile>
                {
                    new ConfigFile
                    {
                        File   = outPath,
                        Status = "ok",
                        Parsed = new List<ConfigBlock>
                        {
                            new ConfigBlock { Directive = "daemon", Args = new List<string> { "off" } }
                        }
                    }
                }
            };
            Crossplane.BuildFiles(payload);
            Assert.That(File.Exists(outPath), Is.True);
            Assert.That(File.ReadAllText(outPath).Trim(), Is.EqualTo("daemon off;"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Crossplane_BuildFiles_RelativePath_UsesCurrentDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ngx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var payload = new ParseResult
            {
                Status = "ok",
                Config = new List<ConfigFile>
                {
                    new ConfigFile
                    {
                        File   = "test-relative.conf",
                        Status = "ok",
                        Parsed = new List<ConfigBlock>
                        {
                            new ConfigBlock { Directive = "pid", Args = new List<string> { "/run/nginx.pid" } }
                        }
                    }
                }
            };
            Crossplane.BuildFiles(payload, dirname: dir);
            Assert.That(File.Exists(Path.Combine(dir, "test-relative.conf")), Is.True);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── round-trips: if-expr ──────────────────────────────────────────────

    [Test]
    public void RoundTrip_IfExpr_IfDirectiveInOutput()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-expr\\nginx.conf" : "if-expr/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("if ("));
        Assert.That(out_, Does.Contain("$slow"));
    }

    [Test]
    public void RoundTrip_IfExpr_IfArgWrappedInParens()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-expr\\nginx.conf" : "if-expr/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        // builder should re-wrap stripped args back in parens for "if"
        Assert.That(out_, Does.Contain("if ($slow)"));
    }

    [Test]
    public void RoundTrip_IfExpr_SetDirectiveInsideIfBlock()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-expr\\nginx.conf" : "if-expr/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("set $var 10;"));
    }

    [Test]
    public void RoundTrip_IfExpr_LocationAndReturnPreserved()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-expr\\nginx.conf" : "if-expr/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("location / {"));
        Assert.That(out_, Does.Contain("return 200"));
    }

    [Test]
    public void RoundTrip_IfExpr_EventsAndHttpPresent()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-expr\\nginx.conf" : "if-expr/nginx.conf";
        var out_ = Build(ParseBlocks(filePath));
        Assert.That(out_, Does.Contain("events {"));
        Assert.That(out_, Does.Contain("worker_connections 1024;"));
        Assert.That(out_, Does.Contain("http {"));
    }

    // ── round-trips: if-check ─────────────────────────────────────────────

    [Test]
    public void RoundTrip_IfCheck_ValidIfBlockPreserved()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-check\\nginx.conf" : "if-check/nginx.conf";
        // CatchErrors=true so the bad if() is absorbed and the rest is parsed
        var blocks = Crossplane.Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true, CatchErrors = true }).Config[0].Parsed;
        var out_ = Build(blocks);
        // the valid if ($something) should appear in output
        Assert.That(out_, Does.Contain("if ($something)"));
        Assert.That(out_, Does.Contain("return 418;"));
    }

    [Test]
    public void RoundTrip_IfCheck_LocationRootReturnPreserved()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-check\\nginx.conf" : "if-check/nginx.conf";
        var blocks = Crossplane.Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true, CatchErrors = true }).Config[0].Parsed;
        var out_ = Build(blocks);
        Assert.That(out_, Does.Contain("location / {"));
        Assert.That(out_, Does.Contain("return 200"));
    }

    [Test]
    public void RoundTrip_IfCheck_ErrorPageDirectivePreserved()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "if-check\\nginx.conf" : "if-check/nginx.conf";
        var blocks = Crossplane.Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true, CatchErrors = true }).Config[0].Parsed;
        var out_ = Build(blocks);
        Assert.That(out_, Does.Contain("error_page"));
        Assert.That(out_, Does.Contain("recursive_error_pages on;"));
    }
}
