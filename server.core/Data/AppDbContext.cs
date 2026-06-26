using Microsoft.EntityFrameworkCore;
using Server.Core.Domain;

namespace Server.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.AppUserId);
            entity.Property(e => e.AppUserId).ValueGeneratedOnAdd();
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
        });

        base.OnModelCreating(modelBuilder);
    }
}
