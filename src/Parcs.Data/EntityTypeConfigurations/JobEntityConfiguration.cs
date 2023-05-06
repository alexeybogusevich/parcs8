using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parcs.Data.Entities;

namespace Parcs.Data.EntityTypeConfigurations
{
    public class JobEntityConfiguration : IEntityTypeConfiguration<JobEntity>
    {
        public void Configure(EntityTypeBuilder<JobEntity> builder)
        {
            builder
                .HasKey(e => e.Id);

            builder
                .Property(e => e.AssemblyName)
                .IsRequired();

            builder
                .Property(e => e.ClassName)
                .IsRequired();

            builder
                .HasOne(e => e.Module)
                .WithMany(e => e.Jobs)
                .HasForeignKey(e => e.ModuleId);

            builder
                .Property(e => e.CreateDateUtc)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}