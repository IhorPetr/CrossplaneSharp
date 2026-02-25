using CrossplaneSharp;
using CrossplaneSharp.Exceptions;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class LexerTests
{
    private static string Fix(string sub) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx",
            sub.Replace('/', Path.DirectorySeparatorChar));

    private static List<NgxToken> Lex(string content) =>
        new NginxLexer().TokenizeContent(content).ToList();

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
            var _ = new NginxLexer().TokenizeContent("worker_processes 4; }").ToList();
        });
        Assert.That(ex!.Strerror, Does.Contain("unexpected"));
    }

    [Test]
    public void UnclosedBlock_ThrowsSyntaxError()
    {
        Assert.Throws<NgxParserSyntaxError>(() =>
        {
            var _ = new NginxLexer().TokenizeContent("events { worker_connections 1024;").ToList();
        });
    }

    [Test]
    public void BalancedBraces_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => { var _ = new NginxLexer().TokenizeContent("events { worker_connections 1024; }").ToList(); });
    }

    // ── fixture: simple ────────────────────────────────────────────────────

    [Test]
    public void Fixture_Simple_ContainsExpectedTokenValues()
    {
        var t = new NginxLexer().Tokenize(Fix("simple/nginx.conf")).ToList();
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
        var t = new NginxLexer().Tokenize(Fix("simple/nginx.conf")).ToList();
        // "foo bar baz" must be a single quoted token
        var q = t.FirstOrDefault(x => x.IsQuoted && x.Value.Contains("foo bar baz"));
        Assert.That(q, Is.Not.Null);
    }

    // ── fixture: with-comments ─────────────────────────────────────────────

    [Test]
    public void Fixture_WithComments_CommentTokensPresent()
    {
        var t = new NginxLexer().Tokenize(Fix("with-comments/nginx.conf")).ToList();
        var comments = t.Where(x => x.Value.StartsWith("#")).ToList();
        Assert.That(comments.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Fixture_WithComments_InlineListenComment()
    {
        var t = new NginxLexer().Tokenize(Fix("with-comments/nginx.conf")).ToList();
        // "#listen" appears on same line as the listen directive (line 7)
        var listenLine = t.First(x => x.Value == "listen").Line;
        Assert.That(t.Any(x => x.Value.StartsWith("#listen") && x.Line == listenLine), Is.True);
    }

    // ── fixture: messy ─────────────────────────────────────────────────────

    [Test]
    public void Fixture_Messy_ParsesWithoutBalanceError()
    {
        Assert.DoesNotThrow(() => { var _ = new NginxLexer().Tokenize(Fix("messy/nginx.conf")).ToList(); });
    }

    [Test]
    public void Fixture_Messy_HasQuotedTokens()
    {
        // messy.conf uses quoted directive names like "events", "http" etc.
        // The lexer should produce tokens with IsQuoted=true for those
        var t = new NginxLexer().Tokenize(Fix("messy/nginx.conf")).ToList();
        Assert.That(t.Any(x => x.IsQuoted), Is.True);
    }

    // ── fixture: russian-text ──────────────────────────────────────────────

    [Test]
    public void Fixture_RussianText_SingleQuotedToken()
    {
        var t = new NginxLexer().Tokenize(Fix("russian-text/nginx.conf")).ToList();
        var q = t.FirstOrDefault(x => x.IsQuoted && x.Value.Contains("русский"));
        Assert.That(q, Is.Not.Null);
    }

    // ── fixture: quoted-right-brace ────────────────────────────────────────

    [Test]
    public void Fixture_QuotedRightBrace_DoesNotThrowBalanceError()
    {
        // the closing "}" inside single-quoted string must not affect brace balance
        Assert.DoesNotThrow(() => { var _ = new NginxLexer().Tokenize(Fix("quoted-right-brace/nginx.conf")).ToList(); });
    }
}


