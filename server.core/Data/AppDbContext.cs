using Microsoft.EntityFrameworkCore;
using Server.Core.Domain;

namespace Server.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        AppUser.Configure(modelBuilder.Entity<AppUser>());

        base.OnModelCreating(modelBuilder);
    }

    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AppUser>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            NormalizeIds(entry.Entity);

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = now;
                entry.Entity.UpdatedUtc = now;
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedUtc = now;
            }
        }
    }

    private static void NormalizeIds(AppUser user)
    {
        user.IamId = NormalizeRequiredId(user.IamId);
        user.EmployeeId = NormalizeOptionalId(user.EmployeeId);
    }

    private static string NormalizeRequiredId(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("IamId is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptionalId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
