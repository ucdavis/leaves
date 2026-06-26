using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Core.Domain;

[Table("AppUser")]
public class AppUser
{
    [Key]
    public int AppUserId { get; set; }

    [Required]
    public Guid EntraObjectId { get; set; }

    [Column(TypeName = "char(10)")]
    [MaxLength(10)]
    public string? IamId { get; set; }

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
    public DateTime CreatedUtc { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; }
}
