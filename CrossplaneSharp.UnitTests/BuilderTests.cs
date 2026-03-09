using System.Runtime.InteropServices;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class BuilderTests
{
    private static readonly string NginxDir =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx");

    private static string Build(List<ConfigBlock> blocks, BuildOptions? opts = null) =>
        new NginxBuilder().Build(blocks, opts);

    private static List<ConfigBlock> ParseBlocks(string fixtureSub) =>
        new NginxParser().Parse(Path.Combine(NginxDir, fixtureSub), new ParseOptions { Single = true })
            .Config[0].Parsed;

    // ── basic output ───────────────────────────────────────────────────────

    [Test]
    public void SimpleDirective_SemicolonTerminated()
    {
        var out_ = Build([new() { Directive = "daemon", Args = ["off"] }]);
        Assert.That(out_.Trim(), Is.EqualTo("daemon off;"));
    }

    [Test]
    public void NoArgs_JustSemicolon()
    {
        var out_ = Build([new() { Directive = "gzip_vary" }]);
        Assert.That(out_.Trim(), Is.EqualTo("gzip_vary;"));
    }

    [Test]
    public void MultipleArgs_SpaceSeparated()
    {
        var out_ = Build([new() { Directive = "error_page", Args = ["404", "/404.html"] }]);
        Assert.That(out_.Trim(), Is.EqualTo("error_page 404 /404.html;"));
    }

    [Test]
    public void BlockDirective_BracesFormatted()
    {
        var out_ = Build([new()
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
        var out_ = Build([new() { Directive = "events", Block = [] }]);
        Assert.That(out_, Does.Contain("events {"));
        Assert.That(out_, Does.Contain("}"));
    }

    // ── indentation ────────────────────────────────────────────────────────

    [Test]
    public void DefaultIndent_FourSpaces()
    {
        var out_ = Build([new()
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
        var blocks = new NginxParser()
            .Parse(Path.Combine(NginxDir, filePath),
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

    // ── BuildFiles ─────────────────────────────────────────────────────────

    [Test]
    public void BuildFiles_WritesFileToDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ngx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var outPath = Path.Combine(dir, "out.conf");
            var payload = new ParseResult
            {
                Status = "ok",
                Config = [new ConfigFile
                {
                    File   = outPath,
                    Status = "ok",
                    Parsed = [new ConfigBlock { Directive = "worker_processes", Args = ["2"] }]
                }]
            };
            new NginxBuilder().BuildFiles(payload);
            var written = File.ReadAllText(outPath);
            Assert.That(written.Trim(), Is.EqualTo("worker_processes 2;"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void BuildFiles_CreatesSubDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ngx_{Guid.NewGuid():N}");
        try
        {
            var outPath = Path.Combine(dir, "sub", "out.conf");
            var payload = new ParseResult
            {
                Status = "ok",
                Config = [new ConfigFile
                {
                    File   = outPath,
                    Status = "ok",
                    Parsed = [new ConfigBlock { Directive = "daemon", Args = ["off"] }]
                }]
            };
            new NginxBuilder().BuildFiles(payload);
            Assert.That(File.Exists(outPath), Is.True);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void BuildFiles_RelativePath_UsesGivenDirname()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ngx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var payload = new ParseResult
            {
                Status = "ok",
                Config = [new ConfigFile
                {
                    File   = "relative.conf",
                    Status = "ok",
                    Parsed = [new ConfigBlock { Directive = "pid", Args = ["/run/nginx.pid"] }]
                }]
            };
            new NginxBuilder().BuildFiles(payload, dirname: dir);
            Assert.That(File.Exists(Path.Combine(dir, "relative.conf")), Is.True);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

