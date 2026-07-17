using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("LeaveRequestDay")]
public class LeaveRequestDay
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LeaveRequestId { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateOnly LeaveDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Hours { get; set; }

    public static void Configure(EntityTypeBuilder<LeaveRequestDay> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.HasIndex(e => new { e.LeaveRequestId, e.LeaveDate })
            .IsUnique()
            .HasDatabaseName("UX_LeaveRequestDay_Request_Date");

        entity.HasIndex(e => e.LeaveDate)
            .HasDatabaseName("IX_LeaveRequestDay_LeaveDate");

        entity.HasOne(e => e.LeaveRequest)
            .WithMany(e => e.Days)
            .HasForeignKey(e => e.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
