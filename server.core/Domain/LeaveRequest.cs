using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("LeaveRequest")]
public class LeaveRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AppUserId { get; set; }

    [Required]
    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public required string IamId { get; set; }

    [Column(TypeName = "char(8)")]
    [MaxLength(8)]
    public string? EmployeeId { get; set; }

    [Required]
    public int LeaveTypeId { get; set; }

    public int? PayLeaveTypeId { get; set; }

    [Required]
    public LeaveRequestStatus Status { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateOnly EndDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalHours { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    [MaxLength(2000)]
    public string? CoveragePlan { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(10)")]
    [MaxLength(10)]
    public required string ReportingDepartmentCodeSnapshot { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ReportingDepartmentNameSnapshot { get; set; }

    public int? ClusterIdSnapshot { get; set; }

    [Required]
    public WorkflowMode WorkflowModeSnapshot { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime SubmittedAt { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<LeaveRequestAction> Actions { get; set; } = new List<LeaveRequestAction>();
    public ICollection<LeaveRequestDay> Days { get; set; } = new List<LeaveRequestDay>();
    public ICollection<OutboundMessage> OutboundMessages { get; set; } = new List<OutboundMessage>();

    public static void Configure(EntityTypeBuilder<LeaveRequest> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.HasIndex(e => e.AppUserId)
            .HasDatabaseName("IX_LeaveRequest_AppUserId");

        entity.HasIndex(e => e.IamId)
            .HasDatabaseName("IX_LeaveRequest_IamId");

        entity.HasIndex(e => e.Status)
            .HasDatabaseName("IX_LeaveRequest_Status");

        entity.HasIndex(e => new { e.ReportingDepartmentCodeSnapshot, e.Status })
            .HasDatabaseName("IX_LeaveRequest_Department_Status");

        entity.HasIndex(e => new { e.StartDate, e.EndDate })
            .HasDatabaseName("IX_LeaveRequest_DateRange");

        entity.HasOne<AppUser>()
            .WithMany(e => e.LeaveRequests)
            .HasForeignKey(e => e.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<LeaveType>()
            .WithMany(e => e.LeaveRequests)
            .HasForeignKey(e => e.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<LeaveType>()
            .WithMany(e => e.PayLeaveRequests)
            .HasForeignKey(e => e.PayLeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
