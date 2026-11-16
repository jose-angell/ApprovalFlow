using ApprovalFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApprovalFlow.Infrastructure.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasMaxLength(Department.NameMaxLength)
            .IsRequired();
        builder.HasIndex(d => d.Name).IsUnique();

        builder.Property(d => d.Description)
            .HasColumnName("description")
            .HasMaxLength(Department.DescriptionMaxLength);

        builder.Property(d => d.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
