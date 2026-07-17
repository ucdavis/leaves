using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("LeaveRequestAction")]
public class LeaveRequestAction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LeaveRequestId { get; set; }

    [Required]
    public LeaveRequestActionType ActionType { get; set; }

    [Required]
    public int ActorAppUserId { get; set; }

    [Required]
    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public required string ActorIamId { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime ActionAt { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    [MaxLength(100)]
    public string? ReasonCode { get; set; }

    [Required]
    public bool IsSelfAction { get; set; } = false;

    public static void Configure(EntityTypeBuilder<LeaveRequestAction> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();
        entity.Property(e => e.IsSelfAction).HasDefaultValue(false);

        entity.HasIndex(e => e.LeaveRequestId)
            .IsUnique()
            .HasDatabaseName("UX_LeaveRequestAction_LeaveRequestId");

        entity.HasIndex(e => e.ActorAppUserId)
            .HasDatabaseName("IX_LeaveRequestAction_ActorAppUserId");

        entity.HasIndex(e => e.ActionAt)
            .HasDatabaseName("IX_LeaveRequestAction_ActionAt");

        entity.HasOne<LeaveRequest>()
            .WithMany(e => e.Actions)
            .HasForeignKey(e => e.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<AppUser>()
            .WithMany(e => e.LeaveRequestActions)
            .HasForeignKey(e => e.ActorAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
