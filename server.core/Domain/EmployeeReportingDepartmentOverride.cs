using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("EmployeeReportingDepartmentOverride")]
public class EmployeeReportingDepartmentOverride
{
    [Key]
    [Column("EmployeeReportingDepartmentOverrideId")]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public required string IamId { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(10)")]
    [MaxLength(10)]
    public required string DepartmentCode { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateOnly EffectiveStartDate { get; set; }

    [Column(TypeName = "date")]
    public DateOnly? EffectiveEndDateExclusive { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Required]
    public int CreatedByAppUserId { get; set; }

    public AppUser? CreatedByAppUser { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public int? ClosedByAppUserId { get; set; }

    public AppUser? ClosedByAppUser { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? ClosedUtc { get; set; }

    public Department? Department { get; set; }

    public static void Configure(EntityTypeBuilder<EmployeeReportingDepartmentOverride> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.HasIndex(e => new { e.IamId, e.EffectiveStartDate, e.EffectiveEndDateExclusive })
            .HasDatabaseName("IX_ReportingDeptOverride_IamId_EffectiveDates");

        entity.HasIndex(e => e.DepartmentCode)
            .HasDatabaseName("IX_ReportingDeptOverride_DepartmentCode");

        entity.HasOne(e => e.Department)
            .WithMany(e => e.EmployeeReportingDepartmentOverrides)
            .HasForeignKey(e => e.DepartmentCode)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByAppUser)
            .WithMany(e => e.CreatedEmployeeReportingDepartmentOverrides)
            .HasForeignKey(e => e.CreatedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ClosedByAppUser)
            .WithMany(e => e.ClosedEmployeeReportingDepartmentOverrides)
            .HasForeignKey(e => e.ClosedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
