using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Parcs.Data.Entities;

namespace Parcs.Data.EntityTypeConfigurations
{
    public class ModuleEntityConfiguration : IEntityTypeConfiguration<ModuleEntity>
    {
        public void Configure(EntityTypeBuilder<ModuleEntity> builder)
        {
            builder
                .HasKey(e => e.Id);

            builder
                .Property(e => e.Name)
                .IsRequired();

            builder
                .Property(e => e.CreateDateUtc)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}