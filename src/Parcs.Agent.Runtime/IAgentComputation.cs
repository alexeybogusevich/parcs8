namespace Parcs.Agent.Runtime;

/// <summary>
/// Implement this interface in user code submitted to the PARCS agent system.
/// Each worker in the parallel job receives an <see cref="AgentLayerInput"/> describing
/// its portion of the work and must return an <see cref="AgentLayerResult"/>.
/// </summary>
public interface IAgentComputation
{
    Task<AgentLayerResult> ExecuteAsync(AgentLayerInput input, CancellationToken cancellationToken = default);
}
