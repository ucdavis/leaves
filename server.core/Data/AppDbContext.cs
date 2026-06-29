using Microsoft.EntityFrameworkCore;
using Server.Core.Domain;

namespace Server.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        AppUser.Configure(modelBuilder.Entity<AppUser>());
    }
}
