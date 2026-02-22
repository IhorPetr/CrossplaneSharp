using System.IO;
using CrossplaneSharp;

namespace CrossplaneSharp.UnitTests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }

    // ── Lexer tests ────────────────────────────────────────────────────────────

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

    // ── Parser tests ───────────────────────────────────────────────────────────

    [Test]
    public void Parser_SimpleConfig_ParsesDirective()
    {
        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, "worker_processes 4;");
            var parser = new NginxParser();
            ParseResult result = parser.Parse(tmpFile);

            Assert.That(result.Status, Is.EqualTo("ok"));
            Assert.That(result.Config, Has.Count.EqualTo(1));
            var directive = result.Config[0].Parsed[0];
            Assert.That(directive.Directive, Is.EqualTo("worker_processes"));
            Assert.That(directive.Args, Has.Count.EqualTo(1));
            Assert.That(directive.Args[0], Is.EqualTo("4"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Test]
    public void Parser_BlockDirective_ParsesNestedBlock()
    {
        const string config = """
            events {
                worker_connections 1024;
            }
            """;

        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, config);
            ParseResult result = new NginxParser().Parse(tmpFile);

            Assert.That(result.Status, Is.EqualTo("ok"));
            var eventsBlock = result.Config[0].Parsed[0];
            Assert.That(eventsBlock.Directive, Is.EqualTo("events"));
            Assert.That(eventsBlock.Block, Is.Not.Null);
            Assert.That(eventsBlock.Block![0].Directive, Is.EqualTo("worker_connections"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Test]
    public void Parser_CommentPreservation_WhenEnabled()
    {
        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, "# top-level comment\nworker_processes 1;");
            var options = new ParseOptions { Comments = true };
            ParseResult result = new NginxParser().Parse(tmpFile, options);

            var comment = result.Config[0].Parsed.FirstOrDefault(b => b.Directive == "#");
            Assert.That(comment, Is.Not.Null);
            Assert.That(comment!.Comment, Is.EqualTo("top-level comment"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── Builder tests ──────────────────────────────────────────────────────────

    [Test]
    public void Builder_SimpleDirective_ReturnsExpectedText()
    {
        var directives = new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "worker_processes", Args = new List<string> { "4" } }
        };

        string output = new NginxBuilder().Build(directives);
        Assert.That(output.Trim(), Is.EqualTo("worker_processes 4;"));
    }

    [Test]
    public void Builder_BlockDirective_ReturnsNestedText()
    {
        var directives = new List<ConfigBlock>
        {
            new ConfigBlock
            {
                Directive = "events",
                Block = new List<ConfigBlock>
                {
                    new ConfigBlock { Directive = "worker_connections", Args = new List<string> { "1024" } }
                }
            }
        };

        string output = new NginxBuilder().Build(directives);
        Assert.That(output, Does.Contain("events {"));
        Assert.That(output, Does.Contain("worker_connections 1024;"));
        Assert.That(output, Does.Contain("}"));
    }

    [Test]
    public void Builder_Comment_IsEmittedWhenEnabled()
    {
        var directives = new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "#", Comment = "my comment" },
            new ConfigBlock { Directive = "worker_processes", Args = new List<string> { "1" } }
        };

        string output = new NginxBuilder().Build(directives, new BuildOptions { IncludeComments = true });
        Assert.That(output, Does.Contain("# my comment"));
    }

    // ── Round-trip test ────────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_ParseThenBuild_ProducesEquivalentConfig()
    {
        const string config = "worker_processes 2;\nevents {\n    worker_connections 512;\n}\n";

        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, config);
            ParseResult result = new NginxParser().Parse(tmpFile);
            string built = new NginxBuilder().Build(result.Config[0].Parsed);

            // verify key directives are present in rebuilt output
            Assert.That(built, Does.Contain("worker_processes 2;"));
            Assert.That(built, Does.Contain("events {"));
            Assert.That(built, Does.Contain("worker_connections 512;"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ── Static entry point (Crossplane class) tests ────────────────────────────

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

    [Test]
    public void Crossplane_Parse_ReturnsResult()
    {
        string tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, "user nginx;");
            ParseResult result = Crossplane.Parse(tmpFile);
            Assert.That(result.Status, Is.EqualTo("ok"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Test]
    public void Crossplane_Build_ReturnsString()
    {
        var directives = new List<ConfigBlock>
        {
            new ConfigBlock { Directive = "user", Args = new List<string> { "nginx" } }
        };
        string output = Crossplane.Build(directives);
        Assert.That(output.Trim(), Is.EqualTo("user nginx;"));
    }
}