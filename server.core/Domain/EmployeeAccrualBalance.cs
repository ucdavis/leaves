using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("EmployeeAccrualBalances")]
public class EmployeeAccrualBalance
{
    public required string EmployeeId { get; set; }
    public DateOnly AsOfDate { get; set; }
    public required string PositionNumber { get; set; }
    public int LeaveTypeNumber { get; set; }
    public string? EmployeeEmail { get; set; }
    public required string EmployeeName { get; set; }
    public required string UnionCode { get; set; }
    public required string UnionDescription { get; set; }
    public required string EmployeeClassCode { get; set; }
    public required string EmployeeClassDescription { get; set; }
    public required string JobCode { get; set; }
    public required string JobCodeDescription { get; set; }
    public string? ReportsToPositionNumber { get; set; }
    public string? ReportsToEmployeeId { get; set; }
    public string? ReportsToEmployeeName { get; set; }
    public required string HrStatus { get; set; }
    public required string EmployeeStatus { get; set; }
    public required string EmployeeStatusDescription { get; set; }
    public required string EmployeeType { get; set; }
    public required string EmployeeTypeDescription { get; set; }
    public decimal HourlyRateFTE { get; set; }
    public required string TypeLabel { get; set; }
    public decimal PrevBal { get; set; }
    public decimal HoursTaken { get; set; }
    public decimal AccrualHours { get; set; }
    public decimal AdjustedHours { get; set; }
    public decimal CalculatedBal { get; set; }
    public decimal AccrualLimit { get; set; }
    public required string ApproachingMax { get; set; }
    public decimal HoursOverUnderPolicyMax { get; set; }
    public decimal AccrualPercentage { get; set; }
    public int ExceptionalMaxVacationOnly { get; set; }
    public required string Level1Dept { get; set; }
    public required string Level1DeptDesc { get; set; }
    public required string Level2Dept { get; set; }
    public required string Level2DeptDesc { get; set; }
    public required string Level3Dept { get; set; }
    public required string Level3DeptDesc { get; set; }
    public required string Level4Dept { get; set; }
    public required string Level4DeptDesc { get; set; }
    public required string Level5Dept { get; set; }
    public required string Level5DeptDesc { get; set; }
    public DateTime? LoadDate { get; set; }
    public DateTime LastUpdated { get; set; }

    public static void Configure(EntityTypeBuilder<EmployeeAccrualBalance> entity)
    {
        entity.ToTable("EmployeeAccrualBalances", "dbo");

        entity.HasKey(e => new { e.EmployeeId, e.AsOfDate, e.PositionNumber, e.LeaveTypeNumber })
            .HasName("PK_EmployeeAccrualBalances");

        entity.Property(e => e.EmployeeId).HasColumnType("nvarchar(11)").HasMaxLength(11);
        entity.Property(e => e.AsOfDate).HasColumnType("date");
        entity.Property(e => e.PositionNumber).HasColumnType("nvarchar(8)").HasMaxLength(8);
        entity.Property(e => e.EmployeeEmail).HasColumnType("nvarchar(320)").HasMaxLength(320);
        entity.Property(e => e.EmployeeName).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.UnionCode).HasColumnType("nvarchar(3)").HasMaxLength(3);
        entity.Property(e => e.UnionDescription).HasColumnType("nvarchar(50)").HasMaxLength(50);
        entity.Property(e => e.EmployeeClassCode).HasColumnType("nvarchar(3)").HasMaxLength(3);
        entity.Property(e => e.EmployeeClassDescription).HasColumnType("nvarchar(50)").HasMaxLength(50);
        entity.Property(e => e.JobCode).HasColumnType("nvarchar(6)").HasMaxLength(6);
        entity.Property(e => e.JobCodeDescription).HasColumnType("nvarchar(50)").HasMaxLength(50);
        entity.Property(e => e.ReportsToPositionNumber).HasColumnType("nvarchar(8)").HasMaxLength(8);
        entity.Property(e => e.ReportsToEmployeeId).HasColumnType("nvarchar(11)").HasMaxLength(11);
        entity.Property(e => e.ReportsToEmployeeName).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.HrStatus).HasColumnType("nvarchar(max)");
        entity.Property(e => e.EmployeeStatus).HasColumnType("nvarchar(max)");
        entity.Property(e => e.EmployeeStatusDescription).HasColumnType("nvarchar(30)").HasMaxLength(30);
        entity.Property(e => e.EmployeeType).HasColumnType("nvarchar(max)");
        entity.Property(e => e.EmployeeTypeDescription).HasColumnType("nvarchar(50)").HasMaxLength(50);
        entity.Property(e => e.HourlyRateFTE).HasColumnType("decimal(12,4)").HasPrecision(12, 4);
        entity.Property(e => e.TypeLabel).HasColumnType("nvarchar(50)").HasMaxLength(50);
        entity.Property(e => e.PrevBal).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.HoursTaken).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.AccrualHours).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.AdjustedHours).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.CalculatedBal).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.AccrualLimit).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.ApproachingMax).HasColumnType("nvarchar(max)");
        entity.Property(e => e.HoursOverUnderPolicyMax).HasColumnType("decimal(10,2)").HasPrecision(10, 2);
        entity.Property(e => e.AccrualPercentage).HasColumnType("decimal(7,2)").HasPrecision(7, 2);
        entity.Property(e => e.Level1Dept).HasColumnType("nvarchar(10)").HasMaxLength(10);
        entity.Property(e => e.Level1DeptDesc).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.Level2Dept).HasColumnType("nvarchar(10)").HasMaxLength(10);
        entity.Property(e => e.Level2DeptDesc).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.Level3Dept).HasColumnType("nvarchar(10)").HasMaxLength(10);
        entity.Property(e => e.Level3DeptDesc).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.Level4Dept).HasColumnType("nvarchar(10)").HasMaxLength(10);
        entity.Property(e => e.Level4DeptDesc).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.Level5Dept).HasColumnType("nvarchar(10)").HasMaxLength(10);
        entity.Property(e => e.Level5DeptDesc).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(e => e.LoadDate).HasColumnType("datetime2(3)").HasPrecision(3);
        entity.Property(e => e.LastUpdated).HasColumnType("datetime2(3)").HasPrecision(3);

        entity.HasIndex(e => e.EmployeeId)
            .HasDatabaseName("IX_EmployeeAccrualBalances_EmployeeId");

        entity.HasIndex(e => new { e.AsOfDate, e.EmployeeId, e.LeaveTypeNumber })
            .HasDatabaseName("IX_EmployeeAccrualBalances_AsOf_Employee_LeaveType");
    }
}
