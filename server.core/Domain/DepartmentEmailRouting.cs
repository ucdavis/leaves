using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("DepartmentEmailRouting")]
public class DepartmentEmailRouting
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(10)")]
    [MaxLength(10)]
    public required string DepartmentCode { get; set; }

    [Required]
    [MaxLength(320)]
    public required string ToEmail { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public int UpdatedByAppUserId { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public static void Configure(EntityTypeBuilder<DepartmentEmailRouting> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => e.DepartmentCode)
            .HasDatabaseName("IX_DepartmentEmailRouting_DepartmentCode");

        entity.HasOne<Department>()
            .WithMany(e => e.DepartmentEmailRoutings)
            .HasForeignKey(e => e.DepartmentCode)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<AppUser>()
            .WithMany(e => e.UpdatedDepartmentEmailRoutings)
            .HasForeignKey(e => e.UpdatedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
