using CrossplaneSharp;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class LexerTests
{
    private string NginxConfPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx", "nginx.conf");

    [Test]
    public void Lexer_SimpleDirective_YieldsExpectedTokens()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.TokenizeContent("worker_processes 4;").ToList();

        Assert.That(tokens, Has.Count.EqualTo(3));
        Assert.That(tokens[0].Value, Is.EqualTo("worker_processes"));
        Assert.That(tokens[0].IsQuoted, Is.False);
        Assert.That(tokens[1].Value, Is.EqualTo("4"));
        Assert.That(tokens[2].Value, Is.EqualTo(";"));
    }

    [Test]
    public void Lexer_QuotedString_IsMarkedQuoted()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.TokenizeContent("server_name \"example.com\";").ToList();

        var quotedToken = tokens.FirstOrDefault(t => t.IsQuoted);
        Assert.That(quotedToken, Is.Not.Null);
        Assert.That(quotedToken!.Value, Is.EqualTo("example.com"));
    }

    [Test]
    public void Lexer_Comment_IsReturnedWithHash()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.TokenizeContent("# this is a comment\nworker_processes 1;").ToList();

        Assert.That(tokens[0].Value, Is.EqualTo("# this is a comment"));
        Assert.That(tokens[0].IsQuoted, Is.False);
    }

    [Test]
    public void Lexer_Braces_AreReturnedAsTokens()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.TokenizeContent("events { }").ToList();

        Assert.That(tokens.Any(t => t.Value == "{"), Is.True);
        Assert.That(tokens.Any(t => t.Value == "}"), Is.True);
    }

    [Test]
    public void Lexer_TrackLineNumbers()
    {
        var lexer = new NginxLexer();
        var tokens = lexer.TokenizeContent("worker_processes 1;\nworker_connections 1024;").ToList();

        var line1Token = tokens.First(t => t.Value == "worker_processes");
        var line2Token = tokens.First(t => t.Value == "worker_connections");

        Assert.That(line1Token.Line, Is.EqualTo(1));
        Assert.That(line2Token.Line, Is.EqualTo(2));
    }

    [Test]
    public void Lexer_NginxConfFixture_ProducesTokens()
    {
        var tokens = new NginxLexer().Tokenize(NginxConfPath).ToList();

        Assert.That(tokens, Is.Not.Empty);
        Assert.That(tokens.Any(t => t.Value == "worker_processes"), Is.True);
        Assert.That(tokens.Any(t => t.Value == "events"), Is.True);
    }

    [Test]
    public void Crossplane_Lex_ReturnsTokens()
    {
        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, "user nginx;");
            var tokens = Crossplane.Lex(tmpFile).ToList();
            Assert.That(tokens, Has.Count.EqualTo(3));
            Assert.That(tokens[0].Value, Is.EqualTo("user"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
