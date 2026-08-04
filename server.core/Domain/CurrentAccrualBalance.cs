using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

public class CurrentAccrualBalance
{
    public required string IamId { get; set; }
    public required string EmployeeId { get; set; }
    public DateOnly LatestAsOfDate { get; set; }
    public int LeaveTypeNumber { get; set; }
    public required string TypeLabel { get; set; }
    public decimal CalculatedBal { get; set; }
    public decimal AccrualLimit { get; set; }
    public required string ApproachingMax { get; set; }
    public decimal AccrualPercentage { get; set; }
    public int PositionRowCount { get; set; }
    public decimal MinCalculatedBal { get; set; }
    public decimal MaxCalculatedBal { get; set; }
    public bool HasDivergentPositionBalances { get; set; }

    public static void Configure(EntityTypeBuilder<CurrentAccrualBalance> entity)
    {
        entity.HasNoKey();
        entity.ToView("vw_CurrentAccrualBalance", "dbo");

        entity.Property(e => e.IamId).HasColumnType("char(10)").HasMaxLength(10);
        entity.Property(e => e.CalculatedBal).HasPrecision(10, 2);
        entity.Property(e => e.AccrualLimit).HasPrecision(10, 2);
        entity.Property(e => e.AccrualPercentage).HasPrecision(7, 2);
        entity.Property(e => e.MinCalculatedBal).HasPrecision(10, 2);
        entity.Property(e => e.MaxCalculatedBal).HasPrecision(10, 2);
    }
}
