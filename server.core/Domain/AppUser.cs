using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("AppUser")]
public class AppUser
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid EntraObjectId { get; set; }

    [Required]
    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public required string IamId { get; set; } 

    [Column(TypeName = "char(8)")]
    [MaxLength(8)]
    public string? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(320)]
    public string? Email { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime FirstLoginUtc { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? LastLoginUtc { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AppAdminAssignment> CreatedAdminAssignments { get; set; } = new List<AppAdminAssignment>();
    public ICollection<Cluster> CreatedClusters { get; set; } = new List<Cluster>();
    public ICollection<DepartmentEmailRouting> UpdatedDepartmentEmailRoutings { get; set; } = new List<DepartmentEmailRouting>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<LeaveRequestAction> LeaveRequestActions { get; set; } = new List<LeaveRequestAction>();

    public static void Configure(EntityTypeBuilder<AppUser> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedOnAdd();
        entity.Property(e => e.IsActive).HasDefaultValue(true);
        entity.HasIndex(e => e.EntraObjectId)
            .IsUnique()
            .HasDatabaseName("UX_AppUser_EntraObjectId");

        entity.HasIndex(e => e.IamId)
            .IsUnique()
            .HasFilter("[IamId] IS NOT NULL")
            .HasDatabaseName("UX_AppUser_IamId");

        entity.HasIndex(e => e.EmployeeId)
            .IsUnique()
            .HasFilter("[EmployeeId] IS NOT NULL")
            .HasDatabaseName("UX_AppUser_EmployeeId");

        entity.HasIndex(e => e.Email)
            .HasDatabaseName("IX_AppUser_Email");
    }
}
