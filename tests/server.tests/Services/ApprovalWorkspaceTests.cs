using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Core.Notification;
using Server.Services;

namespace Server.Tests.Services;

public class ApprovalWorkspaceTests
{
    [Fact]
    public async Task Approval_commits_every_recipient_before_waking_email_delivery()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var approver = new AppUser { IamId = "approver", EntraObjectId = Guid.NewGuid() };
        var requester = new AppUser
        {
            IamId = "requester",
            EntraObjectId = Guid.NewGuid(),
            Email = "requester@example.test",
        };
        db.AppUsers.AddRange(approver, requester);
        db.Departments.Add(new Department { DepartmentCode = "DEPT", DepartmentName = "Department" });
        await db.SaveChangesAsync();
        db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            DepartmentCode = "DEPT",
            IamId = approver.IamId,
            EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            CreatedByAppUserId = approver.Id,
        });
        db.DepartmentEmailRoutings.Add(new DepartmentEmailRouting
        {
            DepartmentCode = "DEPT",
            ToEmail = "routing@example.test",
            UpdatedByAppUserId = approver.Id,
        });
        db.LeaveRequests.Add(new LeaveRequest
        {
            AppUserId = requester.Id,
            IamId = requester.IamId,
            LeaveTypeId = 1,
            Status = LeaveRequestStatus.PendingApproval,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 1),
            TotalHours = 8,
            ReportingDepartmentCodeSnapshot = "DEPT",
            ReportingDepartmentNameSnapshot = "Department",
            WorkflowModeSnapshot = WorkflowMode.ApprovalRequired,
            SubmittedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var wakeSignal = new CommitCheckingWakeSignal(db);
        var queue = new LeaveRequestNotificationQueue(db, NullLogger<LeaveRequestNotificationQueue>.Instance);
        var service = new ApprovalWorkspaceService(null!, db, queue, wakeSignal);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, approver.EntraObjectId.ToString()),
            new Claim(ClaimTypes.Role, "Chair"),
        ], "test"));

        var result = await service.SubmitDecisionAsync(
            principal,
            db.LeaveRequests.Single().Id,
            new SubmitApprovalDecisionRequest("approved", null),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        wakeSignal.SignalCount.Should().Be(1);
        wakeSignal.SawSavedEmailRows.Should().BeTrue();
        db.OutboundMessages.Should().ContainSingle(message => message.RecipientEmail == "requester@example.test");
        db.OutboundMessages.Should().ContainSingle(message => message.RecipientEmail == "routing@example.test");
    }

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
            DepartmentCode = department.DepartmentCode,
            IamId = viewer.IamId,
            EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            CreatedByAppUserId = viewer.Id,
        });
        db.ClusterCaoAssignments.Add(new ClusterCaoAssignment
        {
            ClusterId = cluster.Id,
            IamId = viewer.IamId,
            EffectiveStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            CreatedByAppUserId = viewer.Id,
        });
        void AddRequest(int id, string iamId, LeaveRequestStatus status, bool inScope)
        {
            db.LeaveRequests.Add(new LeaveRequest
            {
                Id = id,
                IamId = iamId,
                AppUserId = former.Id,
                LeaveTypeId = 1,
                Status = status,
                SubmittedAt = new DateTime(2026, 1, 1).AddDays(id),
                StartDate = new DateOnly(2026, 10, 1),
                EndDate = new DateOnly(2026, 10, 2),
                TotalHours = 16,
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
        db.LeaveRequestActions.Add(new LeaveRequestAction
        {
            LeaveRequestId = 5,
            ActionType = LeaveRequestActionType.Approved,
            ActorAppUserId = viewer.Id,
            ActorIamId = viewer.IamId,
            ActionAt = DateTime.UtcNow,
            Comment = "Coverage confirmed.",
        });
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
        workspace.PendingRequests.Select(item => item.Id).Should().Equal(1, 2);
        workspace.PendingRequests.Single(item => item.Id == 1).FacultyName.Should().Be("Former Faculty");
        workspace.PendingRequests.Single(item => item.Id == 2).FacultyName.Should().Be("unnamed");
        workspace.Leaves.Where(item => item.Status == "Approved").Select(item => item.Id).Should().Equal(5);
        var approvedLeave = workspace.Leaves.Single(item => item.Id == 5);
        approvedLeave.Decision.Should().Be("Approved");
        approvedLeave.DecisionBy.Should().Be(viewer.IamId);
        approvedLeave.DecisionComment.Should().Be("Coverage confirmed.");
    }

    private sealed class DirectoryStub(AdminDirectoryData data) : IAdminDirectoryDataService
    {
        public Task<AdminDirectoryData> LoadDirectoryDataAsync(CancellationToken cancellationToken) => Task.FromResult(data);
    }

    private sealed class CommitCheckingWakeSignal(AppDbContext db) : IEmailDeliveryWakeSignal
    {
        public int SignalCount { get; private set; }

        public bool SawSavedEmailRows { get; private set; }

        public void Signal()
        {
            SignalCount++;
            SawSavedEmailRows = db.OutboundMessages.Count() == 2 &&
                db.ChangeTracker.Entries<OutboundMessage>().All(entry => entry.State == EntityState.Unchanged);
        }

        public ValueTask WaitAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
