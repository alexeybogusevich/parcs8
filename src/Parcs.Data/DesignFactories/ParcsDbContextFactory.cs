using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Parcs.Data.Context;

namespace Parcs.Data.DesignFactories
{
    public class ParcsDbContextFactory : IDesignTimeDbContextFactory<ParcsDbContext>
    {
        public ParcsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ParcsDbContext>();
            optionsBuilder.UseNpgsql();

            return new ParcsDbContext(optionsBuilder.Options);
        }
    }
}