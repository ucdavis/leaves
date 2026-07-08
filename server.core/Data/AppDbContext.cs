using Microsoft.EntityFrameworkCore;
using Server.Core.Domain;

namespace Server.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppAdminAssignment> AppAdminAssignments => Set<AppAdminAssignment>();
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        AppUser.Configure(modelBuilder.Entity<AppUser>());
        AppAdminAssignment.Configure(modelBuilder.Entity<AppAdminAssignment>());
        Cluster.Configure(modelBuilder.Entity<Cluster>());
        Department.Configure(modelBuilder.Entity<Department>());
    }
}
