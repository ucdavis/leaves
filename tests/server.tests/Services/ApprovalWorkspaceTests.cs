using System.Security.Claims;
using FluentAssertions;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public class ApprovalWorkspaceTests
{
    [Theory]
    [InlineData("Chair")]
    [InlineData("CAO")]
    public async Task Pending_requests_keep_snapshot_scope_and_name_fallback_while_approved_calendar_requires_current_faculty(string role)
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var viewer = new AppUser { IamId = "viewer", EntraObjectId = Guid.NewGuid() };
        var former = new AppUser { IamId = "former", DisplayName = "Former Faculty", EntraObjectId = Guid.NewGuid() };
        var unnamed = new AppUser { IamId = "unnamed", EntraObjectId = Guid.NewGuid() };
        db.AppUsers.AddRange(viewer, former, unnamed);
        var cluster = new Cluster { Id = 1, ClusterName = "Cluster" };
        var department = new Department { DepartmentCode = "DEPT", DepartmentName = "Department", Cluster = cluster };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            DepartmentCode = department.DepartmentCode, IamId = viewer.IamId,
            EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), CreatedByAppUserId = viewer.Id,
        });
        db.ClusterCaoAssignments.Add(new ClusterCaoAssignment
        {
            ClusterId = cluster.Id, IamId = viewer.IamId,
            EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), CreatedByAppUserId = viewer.Id,
        });
        void AddRequest(int id, string iamId, LeaveRequestStatus status, bool inScope)
        {
            db.LeaveRequests.Add(new LeaveRequest
            {
                Id = id, IamId = iamId, AppUserId = former.Id, LeaveTypeId = 1, Status = status,
                StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 2), TotalHours = 16,
                ReportingDepartmentCodeSnapshot = inScope ? department.DepartmentCode : "OTHER",
                ReportingDepartmentNameSnapshot = inScope ? department.DepartmentName : "Other department",
                ClusterIdSnapshot = inScope ? cluster.Id : 2,
            });
        }
        AddRequest(1, former.IamId, LeaveRequestStatus.PendingApproval, true);
        AddRequest(2, unnamed.IamId, LeaveRequestStatus.PendingApproval, true);
        AddRequest(3, former.IamId, LeaveRequestStatus.PendingApproval, false);
        AddRequest(4, former.IamId, LeaveRequestStatus.Approved, true);
        AddRequest(5, "current", LeaveRequestStatus.Approved, true);
        AddRequest(6, "current", LeaveRequestStatus.PendingApproval, false);
        await db.SaveChangesAsync();
        var data = new AdminDirectoryData(
            AppUsers: [viewer, former, unnamed], Clusters: [cluster],
            CurrentCaoAssignmentsByCluster: new Dictionary<int, ClusterCaoAssignment>(),
            CurrentChairAssignmentsByDepartment: new Dictionary<string, DepartmentChairAssignment>(),
            CurrentFaculty: [new CurrentFacultyWithAccrual
            {
                IamId = "current", DisplayName = "Current Faculty", IsFaculty = true,
                ResolvedReportingDepartmentCode = department.DepartmentCode,
            }],
            CurrentOverridesById: new Dictionary<int, EmployeeReportingDepartmentOverride>(),
            Departments: [department], AdminIamIds: new HashSet<string>());
        var service = new ApprovalWorkspaceService(new DirectoryStub(data), db, null!, null!);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", viewer.EntraObjectId.ToString()),
            new Claim(ClaimTypes.Role, role),
        ], "test"));

        var workspace = await service.GetWorkspaceAsync(principal, default);

        workspace.Should().NotBeNull();
        workspace!.Faculty.Select(item => item.Id).Should().Equal("current");
        workspace.PendingRequests.Select(item => item.Id).Should().BeEquivalentTo([1, 2]);
        workspace.PendingRequests.Single(item => item.Id == 1).FacultyName.Should().Be("Former Faculty");
        workspace.PendingRequests.Single(item => item.Id == 2).FacultyName.Should().Be("unnamed");
        workspace.Leaves.Where(item => item.Status == "Approved").Select(item => item.Id).Should().Equal(5);
    }

    private sealed class DirectoryStub(AdminDirectoryData data) : IAdminDirectoryDataService
    {
        public Task<AdminDirectoryData> LoadDirectoryDataAsync(CancellationToken cancellationToken) => Task.FromResult(data);
    }
}
