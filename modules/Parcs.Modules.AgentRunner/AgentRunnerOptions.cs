using Parcs.Net;
using System.ComponentModel.DataAnnotations;

namespace Parcs.Modules.AgentRunner;

public sealed class AgentRunnerOptions : IModuleOptions
{
    [Range(1, 1000)]
    public int PointsNumber { get; set; } = 1;
}
