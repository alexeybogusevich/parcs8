using Microsoft.EntityFrameworkCore;
using Parcs.Data.Entities;
using System.Reflection;

namespace Parcs.Data.Context
{
    public class ParcsDbContext : DbContext
    {
        public ParcsDbContext(DbContextOptions options)
            : base(options)
        {
            ChangeTracker.LazyLoadingEnabled = false;
        }

        public DbSet<JobEntity> Jobs { get; set; }

        public DbSet<ModuleEntity> Modules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);
        }
    }
}