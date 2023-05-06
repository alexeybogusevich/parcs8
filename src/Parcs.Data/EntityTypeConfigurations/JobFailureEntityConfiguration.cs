using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parcs.Data.Entities;

namespace Parcs.Data.EntityTypeConfigurations
{
    internal class JobFailureEntityConfiguration : IEntityTypeConfiguration<JobFailureEntity>
    {
        public void Configure(EntityTypeBuilder<JobFailureEntity> builder)
        {
            builder
                .HasKey(e => e.Id);

            builder
                .Property(e => e.Message)
                .IsRequired();

            builder
                .Property(e => e.StackTrace)
                .IsRequired();

            builder
                .Property(e => e.CreateDateUtc)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("timezone('utc', now())");

            builder
                .HasOne(e => e.Job)
                .WithMany(e => e.Failures)
                .HasForeignKey(e => e.JobId);
        }
    }
}