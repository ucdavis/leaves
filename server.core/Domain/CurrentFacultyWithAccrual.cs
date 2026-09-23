using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

public class CurrentFacultyWithAccrual
{
    public required string IamId { get; set; }
    public string? EmployeeId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public DateOnly? LatestAsOfDate { get; set; }
    public bool? IsFaculty { get; set; }
    public string? HrStatus { get; set; }
    public string? EmployeeClassCode { get; set; }
    public string? EmployeeClassDescription { get; set; }
    public string? JobCode { get; set; }
    public string? JobCodeDescription { get; set; }
    public string? SourceDepartmentCode { get; set; }
    public string? SourceDepartmentName { get; set; }
    public string? ResolvedReportingDepartmentCode { get; set; }
    public string? ResolvedReportingDepartmentName { get; set; }
    public int? ReportingDepartmentOverrideId { get; set; }
    public bool HasReportingDepartmentOverride { get; set; }

    public static void Configure(EntityTypeBuilder<CurrentFacultyWithAccrual> entity)
    {
        entity.HasNoKey();
        entity.ToView("vw_CurrentFacultyWithAccrual", "dbo");

        entity.Property(e => e.IamId).HasColumnType("char(10)").HasMaxLength(10);
    }
}
