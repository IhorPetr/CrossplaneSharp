using CrossplaneSharp;

namespace CrossplaneSharp.UnitTests;

[TestFixture]
public class BuilderTests
{
    private string NginxConfPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "nginx", "nginx.conf");

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

            Assert.That(built, Does.Contain("worker_processes 2;"));
            Assert.That(built, Does.Contain("events {"));
            Assert.That(built, Does.Contain("worker_connections 512;"));
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Test]
    public void RoundTrip_NginxConfFixture_BuildsWithoutError()
    {
        ParseResult result = new NginxParser().Parse(NginxConfPath,
            new ParseOptions { ParseIncludes = false });

        Assert.That(result.Status, Is.EqualTo("ok"));

        string built = new NginxBuilder().Build(result.Config[0].Parsed);
        Assert.That(built, Does.Contain("worker_processes"));
        Assert.That(built, Does.Contain("events {"));
        Assert.That(built, Does.Contain("http {"));
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
