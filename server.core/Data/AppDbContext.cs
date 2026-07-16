using Microsoft.EntityFrameworkCore;
using Server.Core.Domain;

namespace Server.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppAdminAssignment> AppAdminAssignments => Set<AppAdminAssignment>();
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentEmailRouting> DepartmentEmailRoutings => Set<DepartmentEmailRouting>();
    public IQueryable<EmployeeAccrualBalance> EmployeeAccrualBalances => Set<EmployeeAccrualBalance>().AsNoTracking();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestAction> LeaveRequestActions => Set<LeaveRequestAction>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        AppUser.Configure(modelBuilder.Entity<AppUser>());
        AppAdminAssignment.Configure(modelBuilder.Entity<AppAdminAssignment>());
        Cluster.Configure(modelBuilder.Entity<Cluster>());
        Department.Configure(modelBuilder.Entity<Department>());
        DepartmentEmailRouting.Configure(modelBuilder.Entity<DepartmentEmailRouting>());
        EmployeeAccrualBalance.Configure(modelBuilder.Entity<EmployeeAccrualBalance>());
        LeaveRequest.Configure(modelBuilder.Entity<LeaveRequest>());
        LeaveRequestAction.Configure(modelBuilder.Entity<LeaveRequestAction>());
        LeaveType.Configure(modelBuilder.Entity<LeaveType>());
    }
}
