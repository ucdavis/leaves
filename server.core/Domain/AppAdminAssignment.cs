using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("AppAdminAssignment")]
public class AppAdminAssignment
{
    [Key]
    [Column("AppAdminAssignmentId")]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public required string IamId { get; set; }

    [Required]
    public int CreatedByAppUserId { get; set; }

    public AppUser? CreatedByAppUser { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public static void Configure(EntityTypeBuilder<AppAdminAssignment> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.HasIndex(e => e.IamId)
            .IsUnique()
            .HasDatabaseName("UX_AppAdminAssignment_IamId");

        entity.HasOne(e => e.CreatedByAppUser)
            .WithMany(e => e.CreatedAdminAssignments)
            .HasForeignKey(e => e.CreatedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
