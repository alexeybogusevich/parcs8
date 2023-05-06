using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parcs.Data.Entities;

namespace Parcs.Data.EntityTypeConfigurations
{
    public class JobStatusEntityConfiguration : IEntityTypeConfiguration<JobStatusEntity>
    {
        public void Configure(EntityTypeBuilder<JobStatusEntity> builder)
        {
            builder
                .HasKey(e => e.Id);

            builder
                .HasOne(e => e.Job)
                .WithMany(e => e.Statuses)
                .HasForeignKey(e => e.JobId);

            builder
                .Property(e => e.CreateDateUtc)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}