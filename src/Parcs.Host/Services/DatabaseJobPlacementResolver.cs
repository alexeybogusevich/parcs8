using Microsoft.EntityFrameworkCore;
using Parcs.Core.Services.Interfaces;
using Parcs.Data.Context;

namespace Parcs.Host.Services
{
    public sealed class DatabaseJobPlacementResolver(ParcsDbContext parcsDbContext) : IJobPlacementResolver
    {
        private readonly ParcsDbContext _parcsDbContext = parcsDbContext;

        public Task<bool> RequiresGpuAsync(long jobId, CancellationToken cancellationToken = default) =>
            _parcsDbContext.Jobs
                .Where(j => j.Id == jobId)
                .Select(j => j.RequiresGpu)
                .SingleAsync(cancellationToken);
    }
}
