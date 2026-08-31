using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Core.Domain;

[Table("People")]
public class Person
{
    public required string IamId { get; set; }
    public string? EmployeeId { get; set; }
    public string? StudentId { get; set; }
    public string? ExternalId { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Suffix { get; set; }
    public string? FullName { get; set; }
    public string? Pronouns { get; set; }
    public bool? IsEmployee { get; set; }
    public bool? IsHsEmployee { get; set; }
    public bool? IsFaculty { get; set; }
    public bool? IsStudent { get; set; }
    public bool? IsStaff { get; set; }
    public bool? IsExternal { get; set; }
    public string? PrivacyCode { get; set; }
    public string? IsCampusEmployee { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string? ModifyDateRaw { get; set; }
    public DateTime? FirstIngestedAt { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    public string? LastRunId { get; set; }
    public string? SourceEndpoint { get; set; }
    public DateTime? PromotedAt { get; set; }
    public string? PromotionRunId { get; set; }

    public static void Configure(EntityTypeBuilder<Person> entity)
    {
        entity.ToTable("People", "dbo");

        entity.HasKey(e => e.IamId);

        entity.Property(e => e.IamId).HasColumnType("char(10)").HasMaxLength(10);
        entity.Property(e => e.EmployeeId).HasColumnType("char(8)").HasMaxLength(8);
        entity.Property(e => e.StudentId).HasColumnType("char(9)").HasMaxLength(9);
        entity.Property(e => e.ExternalId).HasColumnType("char(10)").HasMaxLength(10);
        entity.Property(e => e.FirstName).HasColumnType("nvarchar(64)").HasMaxLength(64);
        entity.Property(e => e.MiddleName).HasColumnType("nvarchar(64)").HasMaxLength(64);
        entity.Property(e => e.LastName).HasColumnType("nvarchar(64)").HasMaxLength(64);
        entity.Property(e => e.Suffix).HasColumnType("nvarchar(16)").HasMaxLength(16);
        entity.Property(e => e.FullName).HasColumnType("nvarchar(128)").HasMaxLength(128);
        entity.Property(e => e.Pronouns).HasColumnType("nvarchar(64)").HasMaxLength(64);
        entity.Property(e => e.IsEmployee).HasColumnType("bit");
        entity.Property(e => e.IsHsEmployee).HasColumnType("bit");
        entity.Property(e => e.IsFaculty).HasColumnType("bit");
        entity.Property(e => e.IsStudent).HasColumnType("bit");
        entity.Property(e => e.IsStaff).HasColumnType("bit");
        entity.Property(e => e.IsExternal).HasColumnType("bit");
        entity.Property(e => e.PrivacyCode).HasColumnType("char(1)").HasMaxLength(1);
        entity.Property(e => e.IsCampusEmployee).HasColumnType("char(1)").HasMaxLength(1);
        entity.Property(e => e.UserId).HasColumnType("char(8)").HasMaxLength(8);
        entity.Property(e => e.Email).HasColumnType("varchar(128)").HasMaxLength(128);
        entity.Property(e => e.ModifyDate).HasColumnType("datetime2(6)").HasPrecision(6);
        entity.Property(e => e.ModifyDateRaw).HasColumnType("char(19)").HasMaxLength(19);
        entity.Property(e => e.FirstIngestedAt).HasColumnType("datetime2(6)").HasPrecision(6);
        entity.Property(e => e.LastFetchedAt).HasColumnType("datetime2(6)").HasPrecision(6);
        entity.Property(e => e.LastRunId).HasColumnType("char(36)").HasMaxLength(36);
        entity.Property(e => e.SourceEndpoint).HasColumnType("varchar(128)").HasMaxLength(128);
        entity.Property(e => e.PromotedAt).HasColumnType("datetime2(6)").HasPrecision(6);
        entity.Property(e => e.PromotionRunId).HasColumnType("char(36)").HasMaxLength(36);

        entity.HasIndex(e => e.EmployeeId)
            .HasDatabaseName("IX_People_EmployeeId");
    }
}
