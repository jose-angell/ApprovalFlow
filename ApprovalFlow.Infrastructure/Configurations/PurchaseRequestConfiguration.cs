using ApprovalFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApprovalFlow.Infrastructure.Configurations;

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("purchase_requests");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(p => p.RequestNumber)
            .HasColumnName("request_number")
            .HasMaxLength(PurchaseRequest.RequestNumberMaxLength)
            .IsRequired();
        builder.HasIndex(p => p.RequestNumber).IsUnique();

        builder.Property(p => p.Title)
            .HasColumnName("title")
            .HasMaxLength(PurchaseRequest.TitleMaxLength)
            .IsRequired();

        builder.Property(p => p.Justification)
            .HasColumnName("justification")
            .HasMaxLength(PurchaseRequest.JustificationMaxLength)
            .IsRequired();

        builder.Property(p => p.Comments)
            .HasColumnName("comments")
            .HasMaxLength(PurchaseRequest.CommentsMaxLength);

        builder.Property(p => p.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(p => p.Status);

        builder.Property(p => p.RequesterId).HasColumnName("requester_id");
        builder.Property(p => p.DepartmentId).HasColumnName("department_id");
        builder.Property(p => p.ReviewedById).HasColumnName("reviewed_by_id");
        builder.Property(p => p.ApprovedById).HasColumnName("approved_by_id");
        builder.Property(p => p.RejectedById).HasColumnName("rejected_by_id");
        builder.Property(p => p.CancelledById).HasColumnName("cancelled_by_id");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(p => p.ApprovedAt).HasColumnName("approved_at");
        builder.Property(p => p.RejectedAt).HasColumnName("rejected_at");
        builder.Property(p => p.CancelledAt).HasColumnName("cancelled_at");

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.RejectedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.CancelledById).OnDelete(DeleteBehavior.Restrict);
    }
}
