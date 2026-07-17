using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("Cluster")]
public class Cluster
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ClusterName { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public int? CreatedByAppUserId { get; set; }

    public AppUser? CreatedByAppUser { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<ClusterCaoAssignment> ClusterCaoAssignments { get; set; } = new List<ClusterCaoAssignment>();

    public static void Configure(EntityTypeBuilder<Cluster> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasOne(e => e.CreatedByAppUser)
            .WithMany(e => e.CreatedClusters)
            .HasForeignKey(e => e.CreatedByAppUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
