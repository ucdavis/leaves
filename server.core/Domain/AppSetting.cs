using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("AppSetting")]
public class AppSetting
{
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    [MaxLength(100)]
    public required string SettingKey { get; set; }

    [Required]
    public required string SettingValue { get; set; }

    public int? UpdatedByAppUserId { get; set; }

    public AppUser? UpdatedByAppUser { get; set; }

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public static void Configure(EntityTypeBuilder<AppSetting> entity)
    {
        entity.HasKey(e => e.SettingKey);
        entity.Property(e => e.SettingValue).HasColumnType("nvarchar(max)");

        entity.HasOne(e => e.UpdatedByAppUser)
            .WithMany(e => e.UpdatedAppSettings)
            .HasForeignKey(e => e.UpdatedByAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
