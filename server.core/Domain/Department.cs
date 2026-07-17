using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("Department")]
public class Department
{
    [Key]
    [Column(TypeName = "nvarchar(10)")]
    [MaxLength(10)]
    public required string DepartmentCode { get; set; }

    [Required]
    [MaxLength(100)]
    public required string DepartmentName { get; set; }

    public byte? SourceLevel { get; set; }

    public int? ClusterId { get; set; }

    public Cluster? Cluster { get; set; }

    [Required]
    public WorkflowMode WorkflowMode { get; set; } = WorkflowMode.DirectSubmission;

    [Required]
    public bool IsActive { get; set; } = true;

    public DateTime? LastSeenInSourceAt { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DepartmentEmailRouting> DepartmentEmailRoutings { get; set; } = new List<DepartmentEmailRouting>();
    public ICollection<EmployeeReportingDepartmentOverride> EmployeeReportingDepartmentOverrides { get; set; } = new List<EmployeeReportingDepartmentOverride>();
    public ICollection<DepartmentChairAssignment> DepartmentChairAssignments { get; set; } = new List<DepartmentChairAssignment>();

    public static void Configure(EntityTypeBuilder<Department> entity)
    {
        entity.HasKey(e => e.DepartmentCode);
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.Property(e => e.WorkflowMode).HasDefaultValue(WorkflowMode.DirectSubmission);

        entity.HasIndex(e => e.ClusterId)
            .HasDatabaseName("IX_Department_ClusterId");

        entity.HasIndex(e => e.WorkflowMode)
            .HasDatabaseName("IX_Department_WorkflowMode");

        entity.HasIndex(e => e.LastSeenInSourceAt)
            .HasDatabaseName("IX_Department_LastSeenInSourceAt");

        entity.HasOne(e => e.Cluster)
            .WithMany(e => e.Departments)
            .HasForeignKey(e => e.ClusterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
