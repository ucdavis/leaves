using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("ClusterCaoAssignment")]
public class ClusterCaoAssignment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClusterId { get; set; }

    public Cluster? Cluster { get; set; }

    [Required]
    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public required string IamId { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateOnly EffectiveStartDate { get; set; }

    [Column(TypeName = "date")]
    public DateOnly? EffectiveEndDateExclusive { get; set; }

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

    public static void Configure(EntityTypeBuilder<ClusterCaoAssignment> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();

        entity.HasIndex(e => new { e.ClusterId, e.EffectiveStartDate, e.EffectiveEndDateExclusive })
            .HasDatabaseName("IX_CaoAssignment_Cluster_EffectiveDates");

        entity.HasIndex(e => e.IamId)
            .HasDatabaseName("IX_CaoAssignment_IamId");

        entity.HasOne(e => e.Cluster)
            .WithMany(e => e.ClusterCaoAssignments)
            .HasForeignKey(e => e.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.CreatedByAppUser)
            .WithMany(e => e.CreatedClusterCaoAssignments)
            .HasForeignKey(e => e.CreatedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ClosedByAppUser)
            .WithMany(e => e.ClosedClusterCaoAssignments)
            .HasForeignKey(e => e.ClosedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
