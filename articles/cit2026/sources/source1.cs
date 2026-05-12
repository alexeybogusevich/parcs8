public async Task<AgentLayerResult> ExecuteAsync(
    AgentLayerInput input, CancellationToken ct)
{
    int scenarios = 10_000;
    int n = 50;
    double[,] L = LoadCholeskyFromCustomData(input.CustomData!);
    double[]  w = Enumerable.Repeat(1.0 / n, n).ToArray();

    var rng = new Random(input.WorkerIndex * 1000 + 42);
    var losses = new double[scenarios];
    for (int s = 0; s < scenarios; s++)
        losses[s] = -PortfolioReturn(w, L, rng, n);

    return AgentLayerResult.Ok(JsonSerializer.Serialize(new {
        workerIndex = input.WorkerIndex,
        count       = scenarios,
        losses      = losses
    }));
}