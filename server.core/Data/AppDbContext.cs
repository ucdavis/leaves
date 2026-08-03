using Microsoft.EntityFrameworkCore;
using Server.Core.Domain;

namespace Server.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppAdminAssignment> AppAdminAssignments => Set<AppAdminAssignment>();
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<ClusterCaoAssignment> ClusterCaoAssignments => Set<ClusterCaoAssignment>();
    public IQueryable<CurrentEmployee> CurrentEmployees => Set<CurrentEmployee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentChairAssignment> DepartmentChairAssignments => Set<DepartmentChairAssignment>();
    public DbSet<DepartmentEmailRouting> DepartmentEmailRoutings => Set<DepartmentEmailRouting>();
    public DbSet<EmployeeReportingDepartmentOverride> EmployeeReportingDepartmentOverrides => Set<EmployeeReportingDepartmentOverride>();
    public IQueryable<EmployeeAccrualBalance> EmployeeAccrualBalances => Set<EmployeeAccrualBalance>().AsNoTracking();
    public IQueryable<Person> People => Set<Person>().AsNoTracking();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestAction> LeaveRequestActions => Set<LeaveRequestAction>();
    public DbSet<LeaveRequestDay> LeaveRequestDays => Set<LeaveRequestDay>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<OutboundMessage> OutboundMessages => Set<OutboundMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        AppUser.Configure(modelBuilder.Entity<AppUser>());
        AppAdminAssignment.Configure(modelBuilder.Entity<AppAdminAssignment>());
        Cluster.Configure(modelBuilder.Entity<Cluster>());
        ClusterCaoAssignment.Configure(modelBuilder.Entity<ClusterCaoAssignment>());
        CurrentEmployee.Configure(modelBuilder.Entity<CurrentEmployee>());
        Department.Configure(modelBuilder.Entity<Department>());
        DepartmentChairAssignment.Configure(modelBuilder.Entity<DepartmentChairAssignment>());
        DepartmentEmailRouting.Configure(modelBuilder.Entity<DepartmentEmailRouting>());
        EmployeeReportingDepartmentOverride.Configure(modelBuilder.Entity<EmployeeReportingDepartmentOverride>());
        EmployeeAccrualBalance.Configure(modelBuilder.Entity<EmployeeAccrualBalance>());
        Person.Configure(modelBuilder.Entity<Person>());
        LeaveRequest.Configure(modelBuilder.Entity<LeaveRequest>());
        LeaveRequestAction.Configure(modelBuilder.Entity<LeaveRequestAction>());
        LeaveRequestDay.Configure(modelBuilder.Entity<LeaveRequestDay>());
        LeaveType.Configure(modelBuilder.Entity<LeaveType>());
        OutboundMessage.Configure(modelBuilder.Entity<OutboundMessage>());
    }
}
