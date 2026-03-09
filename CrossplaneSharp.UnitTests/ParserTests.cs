using CrossplaneSharp.Exceptions;
using System.Runtime.InteropServices;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class ParserTests
{
    private static readonly string NginxDir =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx");

    private static ParseResult Parse(string content, ParseOptions? opts = null)
    {
        var tmp = Path.GetTempFileName();
        try   { File.WriteAllText(tmp, content); return Crossplane.Parse(tmp, opts); }
        finally { File.Delete(tmp); }
    }

    // ── result shape ───────────────────────────────────────────────────────

    [Test]
    public void ValidConfig_StatusOk_NoErrors()
    {
        var r = Parse("worker_processes 4;");
        Assert.That(r.Status, Is.EqualTo("ok"));
        Assert.That(r.Errors, Is.Empty);
        Assert.That(r.Config, Has.Count.EqualTo(1));
    }

    [Test]
    public void Directive_Line_And_Args_Captured()
    {
        var r = Parse("worker_processes 4;");
        var stmt = r.Config[0].Parsed[0];
        Assert.That(stmt.Directive, Is.EqualTo("worker_processes"));
        Assert.That(stmt.Args,      Is.EqualTo(new[] { "4" }));
        Assert.That(stmt.Line,      Is.EqualTo(1));
    }

    [Test]
    public void BlockDirective_HasChildBlock()
    {
        var r = Parse("events {\n    worker_connections 1024;\n}");
        var ev = r.Config[0].Parsed[0];
        Assert.That(ev.Directive,     Is.EqualTo("events"));
        Assert.That(ev.Block,         Is.Not.Null);
        Assert.That(ev.Block![0].Directive, Is.EqualTo("worker_connections"));
        Assert.That(ev.Block![0].Args[0],   Is.EqualTo("1024"));
    }

    [Test]
    public void NonBlock_Directive_HasNullBlock()
    {
        var r = Parse("worker_processes 4;");
        Assert.That(r.Config[0].Parsed[0].Block, Is.Null);
    }

    [Test]
    public void MultipleTopLevel_AllPresent()
    {
        var r = Parse("worker_processes 4;\nevents {}\nhttp {}");
        var names = r.Config[0].Parsed.Select(s => s.Directive).ToList();
        Assert.That(names, Is.EqualTo(new[] { "worker_processes", "events", "http" }));
    }

    // ── fixture: simple ────────────────────────────────────────────────────

    [Test]
    public void Fixture_Simple_StatusOk()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_Simple_TopLevelDirectives()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        var names = r.Config[0].Parsed.Select(s => s.Directive).ToList();
        Assert.That(names, Does.Contain("events"));
        Assert.That(names, Does.Contain("http"));
    }

    [Test]
    public void Fixture_Simple_DeepNesting()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var r        = new NginxParser().Parse(Path.Combine(NginxDir, filePath), new ParseOptions { Single = true });
        var http     = r.Config[0].Parsed.First(s => s.Directive == "http");
        var server   = http.Block!.First(s => s.Directive == "server");
        var location = server.Block!.First(s => s.Directive == "location");
        var ret      = location.Block!.First(s => s.Directive == "return");
        Assert.That(ret.Args, Is.EqualTo(new[] { "200", "foo bar baz" }));
    }

    [Test]
    public void Fixture_Simple_ListenAddress()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var r      = new NginxParser().Parse(Path.Combine(NginxDir, filePath), new ParseOptions { Single = true });
        var server = r.Config[0].Parsed.First(s => s.Directive == "http")
                                        .Block!.First(s => s.Directive == "server");
        var listen = server.Block!.First(s => s.Directive == "listen");
        Assert.That(listen.Args[0], Is.EqualTo("127.0.0.1:8080"));
    }

    // ── fixture: with-comments ─────────────────────────────────────────────

    [Test]
    public void Fixture_WithComments_CommentsExcludedByDefault()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "with-comments\\nginx.conf" : "with-comments/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        foreach (var config in r.Config)
            Assert.That(config.Parsed.All(s => s.Directive != "#"), Is.True);
    }

    [Test]
    public void Fixture_WithComments_CommentsIncludedWhenEnabled()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "with-comments\\nginx.conf" : "with-comments/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true, Comments = true });
        var allStmts = Flatten(r.Config[0].Parsed);
        Assert.That(allStmts.Any(s => s.Directive == "#"), Is.True);
    }

    [Test]
    public void Fixture_WithComments_TopLevelCommentText()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "with-comments\\nginx.conf" : "with-comments/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true, Comments = true });
        var comment = r.Config[0].Parsed.First(s => s.Directive == "#" && s.Line == 4);
        Assert.That(comment.Comment, Does.Contain("comment"));
    }

    // ── fixture: comments-between-args ────────────────────────────────────

    [Test]
    public void Fixture_CommentsBetweenArgs_StatusOk()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "comments-between-args\\nginx.conf" : "comments-between-args/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true, Comments = true });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_CommentsBetweenArgs_LogFormatHasTwoArgs()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "comments-between-args\\nginx.conf" : "comments-between-args/nginx.conf";
        var r    = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        var http = r.Config[0].Parsed.First(s => s.Directive == "http");
        var lf   = http.Block!.First(s => s.Directive == "log_format");
        Assert.That(lf.Args.Count, Is.EqualTo(2));
    }

    // ── fixture: bad-args ──────────────────────────────────────────────────

    [Test]
    public void Fixture_BadArgs_CatchErrors_StatusFailed()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "bad-args\\nginx.conf" : "bad-args/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { CatchErrors = true });
        Assert.That(r.Status, Is.EqualTo("failed"));
        Assert.That(r.Errors, Is.Not.Empty);
    }

    [Test]
    public void Fixture_BadArgs_ErrorContainsLineNumber()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "bad-args\\nginx.conf" : "bad-args/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { CatchErrors = true });
        Assert.That(r.Errors[0].Line, Is.Not.Null);
        Assert.That(r.Errors[0].Line, Is.EqualTo(1));
    }

    [Test]
    public void Fixture_BadArgs_ThrowsWhenCatchErrorsFalse()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "bad-args\\nginx.conf" : "bad-args/nginx.conf";
        Assert.Throws<NgxParserDirectiveArgumentsError>(() =>
            new NginxParser().Parse(Path.Combine(NginxDir, filePath),
                new ParseOptions { CatchErrors = false }));
    }

    // ── fixture: missing-semicolon ─────────────────────────────────────────

    [Test]
    public void Fixture_MissingSemicolon_BrokenAbove_StatusFailed()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "missing-semicolon\\broken-above.conf" : "missing-semicolon/broken-above.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { CatchErrors = true });
        Assert.That(r.Status, Is.EqualTo("failed"));
    }

    [Test]
    public void Fixture_MissingSemicolon_BrokenAbove_ParsesContinues()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "missing-semicolon\\broken-above.conf" : "missing-semicolon/broken-above.conf";
        var r        = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { CatchErrors = true });
        var allStmts = Flatten(r.Config[0].Parsed);
        Assert.That(allStmts.Any(s => s.Directive == "location"
            && s.Args.Contains("/not-broken")), Is.True);
    }

    [Test]
    public void Fixture_MissingSemicolon_BrokenBelow_StatusFailed()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "missing-semicolon\\broken-below.conf" : "missing-semicolon/broken-below.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { CatchErrors = true });
        Assert.That(r.Status, Is.EqualTo("failed"));
    }

    // ── fixture: spelling-mistake ──────────────────────────────────────────

    [Test]
    public void Fixture_SpellingMistake_StatusOk_UnknownDirectiveIgnored()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "spelling-mistake\\nginx.conf" : "spelling-mistake/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Strict = false });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_SpellingMistake_StrictMode_StatusFailed()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "spelling-mistake\\nginx.conf" : "spelling-mistake/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Strict = true, CatchErrors = true });
        Assert.That(r.Status, Is.EqualTo("failed"));
        Assert.That(r.Errors[0].Error, Does.Contain("proxy_passs"));
    }

    // ── fixture: includes-regular ──────────────────────────────────────────

    [Test]
    public void Fixture_IncludesRegular_SingleMode_OnlyOneConfig()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-regular\\nginx.conf" : "includes-regular/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        Assert.That(r.Config, Has.Count.EqualTo(1));
    }

    [Test]
    public void Fixture_IncludesRegular_FollowsIncludes_MultipleConfigs()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-regular\\nginx.conf" : "includes-regular/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath));
        Assert.That(r.Config.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Fixture_IncludesRegular_IncludeStmt_HasIncludesIndex()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-regular\\nginx.conf" : "includes-regular/nginx.conf";
        var r       = new NginxParser().Parse(Path.Combine(NginxDir, filePath));
        var http    = r.Config[0].Parsed.First(s => s.Directive == "http");
        var include = http.Block!.First(s => s.Directive == "include");
        Assert.That(include.Includes, Is.Not.Null);
        Assert.That(include.Includes!.Count, Is.EqualTo(1));
        Assert.That(include.Includes![0], Is.EqualTo(1));
    }

    [Test]
    public void Fixture_IncludesRegular_FooConf_LocationFoo()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-regular\\nginx.conf" : "includes-regular/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath));
        var fooConfig = r.Config.FirstOrDefault(c => c.File.EndsWith("foo.conf")
            && !c.File.Contains("conf.d"));
        Assert.That(fooConfig, Is.Not.Null);
        Assert.That(fooConfig!.Parsed[0].Directive, Is.EqualTo("location"));
        Assert.That(fooConfig.Parsed[0].Args[0],    Is.EqualTo("/foo"));
    }

    // ── fixture: includes-globbed ──────────────────────────────────────────

    [Test]
    public void Fixture_IncludesGlobbed_StatusOk()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-globbed\\nginx.conf" : "includes-globbed/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath));
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_IncludesGlobbed_ExpandsMultipleFiles()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-globbed\\nginx.conf" : "includes-globbed/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath));
        Assert.That(r.Config.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Fixture_IncludesGlobbed_BothServersPresent()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-globbed\\nginx.conf" : "includes-globbed/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath));
        var files = r.Config.Select(c => Path.GetFileName(c.File)).ToList();
        Assert.That(files, Does.Contain("server1.conf"));
        Assert.That(files, Does.Contain("server2.conf"));
    }

    // ── fixture: directive-with-space ─────────────────────────────────────

    [Test]
    public void Fixture_DirectiveWithSpace_MapParsed()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "directive-with-space\\nginx.conf" : "directive-with-space/nginx.conf";
        var r    = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        Assert.That(r.Status, Is.EqualTo("ok"));
        var http = r.Config[0].Parsed.First(s => s.Directive == "http");
        var map  = http.Block!.First(s => s.Directive == "map");
        Assert.That(map.Args[0], Is.EqualTo("$http_user_agent"));
        Assert.That(map.Args[1], Is.EqualTo("$mobile"));
    }

    [Test]
    public void Fixture_DirectiveWithSpace_OperaMiniIsQuotedArg()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "directive-with-space\\nginx.conf" : "directive-with-space/nginx.conf";
        var r    = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        var http = r.Config[0].Parsed.First(s => s.Directive == "http");
        var map  = http.Block!.First(s => s.Directive == "map");
        var entry = map.Block!.FirstOrDefault(s => s.Directive.Contains("Opera Mini"));
        Assert.That(entry, Is.Not.Null, "Opera Mini entry should be in map block");
        Assert.That(entry!.Args[0], Is.EqualTo("1"));
    }

    // ── fixture: empty-value-map ───────────────────────────────────────────

    [Test]
    public void Fixture_EmptyValueMap_StatusOk()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "empty-value-map\\nginx.conf" : "empty-value-map/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_EmptyValueMap_EmptyStringArgs()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "empty-value-map\\nginx.conf" : "empty-value-map/nginx.conf";
        var r    = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        var http = r.Config[0].Parsed.First(s => s.Directive == "http");
        var map  = http.Block!.First(s => s.Directive == "map");
        var first = map.Block![0];
        Assert.That(first.Directive, Is.EqualTo(""));
        Assert.That(first.Args[0],   Is.EqualTo("$arg"));
        var second = map.Block![1];
        Assert.That(second.Directive, Is.EqualTo("*.example.com"));
        Assert.That(second.Args[0],   Is.EqualTo(""));
    }

    // ── fixture: quoted-right-brace ────────────────────────────────────────

    [Test]
    public void Fixture_QuotedRightBrace_StatusOk()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "quoted-right-brace\\nginx.conf" : "quoted-right-brace/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_QuotedRightBrace_LogFormatHasManyArgs()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "quoted-right-brace\\nginx.conf" : "quoted-right-brace/nginx.conf";
        var r    = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        var http = r.Config[0].Parsed.First(s => s.Directive == "http");
        var lf   = http.Block!.First(s => s.Directive == "log_format");
        Assert.That(lf.Args.Count, Is.GreaterThanOrEqualTo(3));
    }

    // ── fixture: russian-text ──────────────────────────────────────────────

    [Test]
    public void Fixture_RussianText_StatusOk()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "russian-text\\nginx.conf" : "russian-text/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    [Test]
    public void Fixture_RussianText_EnvArgIsRussian()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "russian-text\\nginx.conf" : "russian-text/nginx.conf";
        var r   = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Single = true });
        var env = r.Config[0].Parsed.First(s => s.Directive == "env");
        Assert.That(env.Args[0], Does.Contain("русский"));
    }

    // ── fixture: messy ─────────────────────────────────────────────────────

    [Test]
    public void Fixture_Messy_ParsesWithoutThrow()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "messy\\nginx.conf" : "messy/nginx.conf";
        Assert.DoesNotThrow(() =>
            new NginxParser().Parse(Path.Combine(NginxDir, filePath),
                new ParseOptions { Single = true, CatchErrors = true }));
    }

    // ── if-block arg stripping ─────────────────────────────────────────────

    [Test]
    public void IfBlock_ParenthesesStripped_FromArgs()
    {
        var r      = Parse("server { listen 80; if ($request_method = POST) { return 405; } }",
            new ParseOptions { Single = true, CheckCtx = false });
        var server = r.Config[0].Parsed.First(s => s.Directive == "server");
        var ifBlk  = server.Block!.First(s => s.Directive == "if");
        Assert.That(ifBlk.Args.Any(a => a.StartsWith("(") || a.EndsWith(")")), Is.False);
        Assert.That(string.Join(" ", ifBlk.Args), Does.Contain("$request_method"));
    }

    // ── combine mode ───────────────────────────────────────────────────────

    [Test]
    public void Combine_SingleConfigEntry()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-regular\\nginx.conf" : "includes-regular/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Combine = true });
        Assert.That(r.Config, Has.Count.EqualTo(1));
    }

    [Test]
    public void Combine_MergesIncludedStatements()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "includes-regular\\nginx.conf" : "includes-regular/nginx.conf";
        var r = new NginxParser().Parse(Path.Combine(NginxDir, filePath),
            new ParseOptions { Combine = true });
        var allNames = Flatten(r.Config[0].Parsed).Select(s => s.Directive).ToList();
        Assert.That(allNames, Contains.Item("server"));
        Assert.That(allNames, Contains.Item("location"));
    }

    // ── ignore option ──────────────────────────────────────────────────────

    [Test]
    public void Ignore_DirectiveSkipped()
    {
        var r = Parse("worker_processes 4;\npid /run/nginx.pid;",
            new ParseOptions { Ignore = new HashSet<string> { "pid" } });
        Assert.That(r.Config[0].Parsed.Any(s => s.Directive == "pid"),            Is.False);
        Assert.That(r.Config[0].Parsed.Any(s => s.Directive == "worker_processes"), Is.True);
    }

    // ── context checking ───────────────────────────────────────────────────

    [Test]
    public void WrongContext_CatchErrors_StatusFailed()
    {
        // worker_connections is only valid inside events{}
        var r = Parse("worker_connections 1024;",
            new ParseOptions { CatchErrors = true, CheckCtx = true });
        Assert.That(r.Status, Is.EqualTo("failed"));
    }

    [Test]
    public void WrongContext_ThrowsWhenNotCatching()
    {
        Assert.Throws<NgxParserDirectiveContextError>(() =>
            Parse("worker_connections 1024;",
                new ParseOptions { CatchErrors = false, CheckCtx = true }));
    }

    [Test]
    public void CheckCtxDisabled_NoError()
    {
        var r = Parse("worker_connections 1024;",
            new ParseOptions { CheckCtx = false });
        Assert.That(r.Status, Is.EqualTo("ok"));
    }

    // ── onerror callback ───────────────────────────────────────────────────

    [Test]
    public void OnError_CallbackFiredAndReturnValueStored()
    {
        bool called = false;
        var r = Parse("worker_connections 1024;",
            new ParseOptions
            {
                CatchErrors = true, CheckCtx = true,
                OnError = _ => { called = true; return "cb-result"; }
            });
        Assert.That(called, Is.True);
        Assert.That(r.Errors[0].Callback, Is.EqualTo("cb-result"));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static IEnumerable<ConfigBlock> Flatten(IEnumerable<ConfigBlock> blocks)
    {
        foreach (var b in blocks)
        {
            yield return b;
            if (b.Block != null)
                foreach (var child in Flatten(b.Block))
                    yield return child;
        }
    }
}


