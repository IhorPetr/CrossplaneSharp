using CrossplaneSharp.Exceptions;
using System.Runtime.InteropServices;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class LexerTests
{
    private static readonly string NginxDir =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx");

    private static List<NgxToken> Lex(string content) =>
        Crossplane.LexString(content).ToList();

    // ── basic tokenisation ─────────────────────────────────────────────────

    [Test]
    public void EmptyString_NoTokens()
    {
        Assert.That(Lex(""), Is.Empty);
    }

    [Test]
    public void WhitespaceOnly_NoTokens()
    {
        Assert.That(Lex("  \t\n  "), Is.Empty);
    }

    [Test]
    public void SimpleDirective_ThreeTokens()
    {
        var t = Lex("daemon off;");
        Assert.That(t.Count, Is.EqualTo(3));
        Assert.That(t[0].Value, Is.EqualTo("daemon"));
        Assert.That(t[1].Value, Is.EqualTo("off"));
        Assert.That(t[2].Value, Is.EqualTo(";"));
        Assert.That(t.All(x => !x.IsQuoted), Is.True);
    }

    [Test]
    public void Braces_ReturnedAsSeparateTokens()
    {
        var t = Lex("events { }");
        Assert.That(t.Any(x => x.Value == "{"), Is.True);
        Assert.That(t.Any(x => x.Value == "}"), Is.True);
    }

    // ── line numbers ───────────────────────────────────────────────────────

    [Test]
    public void LineNumbers_StartAtOne()
    {
        var t = Lex("a 1;");
        Assert.That(t[0].Line, Is.EqualTo(1));
    }

    [Test]
    public void LineNumbers_IncrementOnNewline()
    {
        var t = Lex("a 1;\nb 2;\nc 3;");
        Assert.That(t.First(x => x.Value == "a").Line, Is.EqualTo(1));
        Assert.That(t.First(x => x.Value == "b").Line, Is.EqualTo(2));
        Assert.That(t.First(x => x.Value == "c").Line, Is.EqualTo(3));
    }

    // ── quoted strings ─────────────────────────────────────────────────────

    [Test]
    public void DoubleQuotedString_MarkedQuoted_StripsBraces()
    {
        var t = Lex("server_name \"example.com\";");
        var q = t.First(x => x.IsQuoted);
        Assert.That(q.Value, Is.EqualTo("example.com"));
    }

    [Test]
    public void SingleQuotedString_MarkedQuoted()
    {
        var t = Lex("return 200 'foo';");
        var q = t.First(x => x.IsQuoted);
        Assert.That(q.Value, Is.EqualTo("foo"));
        Assert.That(q.IsQuoted, Is.True);
    }

    [Test]
    public void EmptyDoubleQuote_MarkedQuotedEmptyValue()
    {
        var t = Lex("set $v \"\";");
        var q = t.First(x => x.IsQuoted);
        Assert.That(q.Value, Is.EqualTo(""));
    }

    // ── comments ──────────────────────────────────────────────────────────

    [Test]
    public void Comment_ReturnedWithLeadingHash()
    {
        var t = Lex("# hello\nworker_processes 1;");
        Assert.That(t[0].Value, Is.EqualTo("# hello"));
        Assert.That(t[0].IsQuoted, Is.False);
        Assert.That(t[0].Line, Is.EqualTo(1));
    }

    [Test]
    public void InlineComment_IsOwnToken()
    {
        var t = Lex("listen 80; # inline\n");
        Assert.That(t.Any(x => x.Value.StartsWith("# inline")), Is.True);
    }

    // ── variable expansion ─────────────────────────────────────────────────

    [Test]
    public void DollarPlainVar_IncludedInToken()
    {
        var t = Lex("proxy_set_header X-IP $remote_addr;");
        Assert.That(t.Any(x => x.Value == "$remote_addr"), Is.True);
    }

    // ── brace balance errors ───────────────────────────────────────────────

    [Test]
    public void UnexpectedClosingBrace_ThrowsSyntaxError()
    {
        var ex = Assert.Throws<NgxParserSyntaxError>(() =>
        {
            var _ = Crossplane.LexString("worker_processes 4; }").ToList();
        });
        Assert.That(ex!.Strerror, Does.Contain("unexpected"));
    }

    [Test]
    public void UnclosedBlock_ThrowsSyntaxError()
    {
        Assert.Throws<NgxParserSyntaxError>(() =>
        {
            var _ = Crossplane.LexString("events { worker_connections 1024;").ToList();
        });
    }

    [Test]
    public void BalancedBraces_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => { var _ = Crossplane.LexString("events { worker_connections 1024; }").ToList(); });
    }

    // ── fixture: simple ────────────────────────────────────────────────────

    [Test]
    public void Fixture_Simple_ContainsExpectedTokenValues()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var t = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList();
        Assert.That(t.Any(x => x.Value == "events"),             Is.True);
        Assert.That(t.Any(x => x.Value == "worker_connections"), Is.True);
        Assert.That(t.Any(x => x.Value == "http"),               Is.True);
        Assert.That(t.Any(x => x.Value == "server"),             Is.True);
        Assert.That(t.Any(x => x.Value == "listen"),             Is.True);
        Assert.That(t.Any(x => x.Value == "location"),           Is.True);
        Assert.That(t.Any(x => x.Value == "return"),             Is.True);
    }

    [Test]
    public void Fixture_Simple_QuotedReturnValue()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var t = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList();
        // "foo bar baz" must be a single quoted token
        var q = t.FirstOrDefault(x => x.IsQuoted && x.Value.Contains("foo bar baz"));
        Assert.That(q, Is.Not.Null);
    }

    // ── fixture: with-comments ─────────────────────────────────────────────

    [Test]
    public void Fixture_WithComments_CommentTokensPresent()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "with-comments\\nginx.conf" : "with-comments/nginx.conf";
        var t = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList();
        var comments = t.Where(x => x.Value.StartsWith("#")).ToList();
        Assert.That(comments.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Fixture_WithComments_InlineListenComment()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "with-comments\\nginx.conf" : "with-comments/nginx.conf";
        var t = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList();
        // "#listen" appears on same line as the listen directive (line 7)
        var listenLine = t.First(x => x.Value == "listen").Line;
        Assert.That(t.Any(x => x.Value.StartsWith("#listen") && x.Line == listenLine), Is.True);
    }

    // ── fixture: messy ─────────────────────────────────────────────────────

    [Test]
    public void Fixture_Messy_ParsesWithoutBalanceError()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "messy\\nginx.conf" : "messy/nginx.conf";
        Assert.DoesNotThrow(() => { var _ = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList(); });
    }

    [Test]
    public void Fixture_Messy_HasQuotedTokens()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "messy\\nginx.conf" : "messy/nginx.conf";
        var t = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList();
        Assert.That(t.Any(x => x.IsQuoted), Is.True);
    }

    // ── fixture: russian-text ──────────────────────────────────────────────

    [Test]
    public void Fixture_RussianText_SingleQuotedToken()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "russian-text\\nginx.conf" : "russian-text/nginx.conf";
        var t = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList();
        var q = t.FirstOrDefault(x => x.IsQuoted && x.Value.Contains("русский"));
        Assert.That(q, Is.Not.Null);
    }

    // ── fixture: quoted-right-brace ────────────────────────────────────────

    [Test]
    public void Fixture_QuotedRightBrace_DoesNotThrowBalanceError()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "quoted-right-brace\\nginx.conf" : "quoted-right-brace/nginx.conf";
        Assert.DoesNotThrow(() => { var _ = Crossplane.Lex(Path.Combine(NginxDir, filePath)).ToList(); });
    }

    // ── NginxLexer direct API ──────────────────────────────────────────────

    [Test]
    public void NginxLexer_Lex_ReturnsTokenList()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var lexer = new NginxLexer();
        var tokens = lexer.Lex(Path.Combine(NginxDir, filePath));
        Assert.That(tokens, Is.Not.Empty);
        Assert.That(tokens.Any(t => t.Value == "http"), Is.True);
    }

    [Test]
    public void NginxLexer_LexString_ReturnsTokens()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.LexString("gzip on;");
        Assert.That(tokens.Count, Is.EqualTo(3));
        Assert.That(tokens[0].Value, Is.EqualTo("gzip"));
    }

    [Test]
    public void NginxLexer_LexString_WithFilename_DoesNotThrow()
    {
        var lexer = new NginxLexer();
        Assert.DoesNotThrow(() => lexer.LexString("worker_processes 2;", "test.conf"));
    }

    [Test]
    public void NginxLexer_TokenizeContent_ReturnsLazySequence()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.TokenizeContent("server_name example.com;").ToList();
        Assert.That(tokens.Any(t => t.Value == "server_name"), Is.True);
    }

    // ── Crossplane.Lex / LexString entry-points ───────────────────────────

    [Test]
    public void Crossplane_Lex_ReturnsTokensFromFile()
    {
        var filePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "simple\\nginx.conf" : "simple/nginx.conf";
        var tokens = Crossplane.Lex(Path.Combine(NginxDir, filePath));
        Assert.That(tokens, Is.Not.Empty);
        Assert.That(tokens.Any(t => t.Value == "events"), Is.True);
    }

    [Test]
    public void Crossplane_LexString_ReturnsTokensFromString()
    {
        var tokens = Crossplane.LexString("worker_processes 4;");
        Assert.That(tokens.Count, Is.EqualTo(3));
        Assert.That(tokens[0].Value, Is.EqualTo("worker_processes"));
    }

    [Test]
    public void Crossplane_LexString_WithFilename_DoesNotThrow()
    {
        var tokens = Crossplane.LexString("pid /run/nginx.pid;", "virtual.conf");
        Assert.That(tokens, Is.Not.Empty);
    }

    // ── NgxToken ──────────────────────────────────────────────────────────

    [Test]
    public void NgxToken_ToString_ContainsValue()
    {
        var token = new NgxToken("listen", 1, false);
        var str = token.ToString();
        Assert.That(str, Does.Contain("listen"));
    }
}
