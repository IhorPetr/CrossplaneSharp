using CrossplaneSharp;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class ParserTests
{
    private string NginxConfPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx", "nginx.conf");

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

    [Test]
    public void Parser_NginxConfFixture_ParsesSuccessfully()
    {
        ParseResult result = new NginxParser().Parse(NginxConfPath,
            new ParseOptions { ParseIncludes = false });

        Assert.That(result.Status, Is.EqualTo("ok"));
        var directives = result.Config[0].Parsed;
        Assert.That(directives.Any(d => d.Directive == "worker_processes"), Is.True);
        Assert.That(directives.Any(d => d.Directive == "events"), Is.True);
        Assert.That(directives.Any(d => d.Directive == "http"), Is.True);
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
}
