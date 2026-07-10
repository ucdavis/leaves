using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("LeaveType")]
public class LeaveType
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string LeaveTypeKey { get; set; }

    public int? SourceLeaveTypeNumber { get; set; }

    [Required]
    [MaxLength(100)]
    public required string DisplayName { get; set; }

// missing default values 
    [Required]
    public bool HasAccrualBalance { get; set; } = false;

    [Required]
    public bool IsActive { get; set; } = true;

    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<LeaveRequest> PayLeaveRequests { get; set; } = new List<LeaveRequest>();

    public static void Configure(EntityTypeBuilder<LeaveType> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();
        entity.Property(e => e.HasAccrualBalance).HasDefaultValue(false);
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => e.LeaveTypeKey)
            .IsUnique()
            .HasDatabaseName("UX_LeaveType_LeaveTypeKey");

        entity.HasIndex(e => e.SourceLeaveTypeNumber)
            .HasDatabaseName("IX_LeaveType_SourceLeaveTypeNumber");
    }
}
