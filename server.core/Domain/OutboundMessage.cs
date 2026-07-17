using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("OutboundMessage")]
public class OutboundMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LeaveRequestId { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }

    [Required]
    [MaxLength(100)]
    public required string NotificationType { get; set; }

    [Required]
    [MaxLength(320)]
    public required string RecipientEmail { get; set; }

    [Required]
    public OutboundMessageStatus Status { get; set; }

    [Required]
    [MaxLength(450)]
    public required string DedupeKey { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime NotBeforeUtc { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? LockedUntilUtc { get; set; }

    public Guid? LockId { get; set; }

    [Required]
    public int AttemptCount { get; set; } = 0;

    [MaxLength(2000)]
    public string? LastError { get; set; }

    [MaxLength(200)]
    public string? ProviderMessageId { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "datetime2")]
    public DateTime? SentUtc { get; set; }

    public static void Configure(EntityTypeBuilder<OutboundMessage> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();
        entity.Property(e => e.AttemptCount).HasDefaultValue(0);

        entity.HasIndex(e => e.DedupeKey)
            .IsUnique()
            .HasDatabaseName("UX_OutboundMessage_DedupeKey");

        entity.HasIndex(e => new { e.Status, e.NotBeforeUtc, e.LockedUntilUtc })
            .HasDatabaseName("IX_OutboundMessage_Status_NotBefore_LockedUntil");

        entity.HasIndex(e => e.LeaveRequestId)
            .HasDatabaseName("IX_OutboundMessage_LeaveRequestId");

        entity.HasIndex(e => e.CreatedUtc)
            .HasDatabaseName("IX_OutboundMessage_CreatedUtc");

        entity.HasIndex(e => e.NotificationType)
            .HasDatabaseName("IX_OutboundMessage_NotificationType");

        entity.HasOne(e => e.LeaveRequest)
            .WithMany(e => e.OutboundMessages)
            .HasForeignKey(e => e.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
