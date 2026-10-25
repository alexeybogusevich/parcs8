using Parcs.Agent.Mcp.Models;
using Parcs.Agent.Mcp.Tools;

namespace Parcs.Agent.Mcp.Tests;

public class ToolArgumentsTests
{
    [Test]
    public void CorrectArgumentsPass() => Assert.Multiple(() =>
    {
        Assert.That(ToolArguments.Check("create_session", ["sourceCode"]), Is.Null);
        Assert.That(ToolArguments.Check("run_layer", ["sessionId", "parallelism", "failFast", "view"]), Is.Null);
        Assert.That(ToolArguments.Check("get_cluster_info", null), Is.Null);
    });

    [Test]
    public void AMisspeltArgumentIsNamedWithTheIntendedOne()
    {
        var error = ToolArguments.Check("create_session", ["source_code"])!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(ErrorCodes.InvalidArgument));
            Assert.That(error.Message, Does.Contain("missing required argument 'sourceCode'"));
            Assert.That(error.Message, Does.Contain("unknown argument 'source_code' (did you mean 'sourceCode'?)"));
        });
    }

    [Test]
    public void ArgumentsTheToolDoesNotTakeAreRejected()
    {
        var error = ToolArguments.Check("run_layer", ["sessionId", "parallelism", "topology"])!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Message, Does.Contain("unknown argument 'topology'"));
            Assert.That(error.Message, Does.Not.Contain("did you mean"));
            Assert.That(error.Message, Does.Contain("'sessionId', 'parallelism' required"));
        });
    }

    [Test]
    public void UnknownToolsAreLeftToTheServer() =>
        Assert.That(ToolArguments.Check("no_such_tool", ["x"]), Is.Null);

    [Test]
    public void TheCancellationTokenIsNotAnArgument() =>
        Assert.That(ToolArguments.Check("wait_layer", ["layerId", "ct"])!.Message, Does.Contain("unknown argument 'ct'"));
}
