using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Server.Controllers;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Core.Notification;
using Server.Services;

namespace Server.Tests.Controllers;

public class AdminDepartmentsControllerWorkflowTests
{
    [Fact]
    public async Task CreateDepartment_adds_the_default_request_notification_email()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = new AppUser
        {
            EntraObjectId = Guid.NewGuid(),
            IamId = "admin",
            FirstLoginUtc = DateTime.UtcNow,
        };
        db.AppUsers.Add(admin);
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin, "service@example.test");

        var result = await controller.CreateDepartment(
            new AdminDepartmentsController.CreateDepartmentRequest(
                ApprovalMode: "approval",
                ClusterId: null,
                Code: "DEPT",
                Name: "Department"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        var routingEmail = db.DepartmentEmailRoutings.Should().ContainSingle().Subject;
        routingEmail.DepartmentCode.Should().Be("DEPT");
        routingEmail.ToEmail.Should().Be("service@example.test");
        routingEmail.IsActive.Should().BeTrue();
        routingEmail.UpdatedByAppUserId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task CreateDepartment_allows_the_name_of_an_inactive_department_when_the_code_is_new()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Departments.Add(new Department
        {
            DepartmentCode = "OLD",
            DepartmentName = "Agricultural Sciences",
            IsActive = false,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var result = await controller.CreateDepartment(
            new AdminDepartmentsController.CreateDepartmentRequest(
                ApprovalMode: "auto",
                ClusterId: null,
                Code: "NEW",
                Name: "Agricultural Sciences"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await db.Departments.IgnoreQueryFilters().ToListAsync())
            .Should()
            .Contain(department => department.DepartmentCode == "NEW" && department.IsActive);
    }

    [Fact]
    public async Task CreateDepartment_rejects_the_name_of_an_active_department()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Departments.Add(new Department
        {
            DepartmentCode = "EXISTING",
            DepartmentName = "Agricultural Sciences",
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var result = await controller.CreateDepartment(
            new AdminDepartmentsController.CreateDepartmentRequest(
                ApprovalMode: "auto",
                ClusterId: null,
                Code: "NEW",
                Name: "Agricultural Sciences"),
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be("An active department with that name already exists.");
    }

    [Fact]
    public async Task UpdateDepartment_rejects_the_name_of_another_active_department()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Departments.AddRange(
            new Department
            {
                DepartmentCode = "EXISTING",
                DepartmentName = "Agricultural Sciences",
            },
            new Department
            {
                DepartmentCode = "TARGET",
                DepartmentName = "Target Department",
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var result = await controller.UpdateDepartment(
            "TARGET",
            new AdminDepartmentsController.UpdateDepartmentRequest(
                Name: "Agricultural Sciences",
                ClusterId: null,
                ClusterIdSet: false,
                ApprovalMode: null,
                ApprovePendingRequests: false,
                ChairUserId: null,
                ChairUserIdSet: false),
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be("An active department with that name already exists.");
    }

    [Fact]
    public async Task CreateDepartment_returns_a_dedicated_conflict_for_an_inactive_code()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Departments.Add(new Department
        {
            DepartmentCode = "DEPT",
            DepartmentName = "Former Department",
            IsActive = false,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var result = await controller.CreateDepartment(
            new AdminDepartmentsController.CreateDepartmentRequest(
                ApprovalMode: "auto",
                ClusterId: null,
                Code: "DEPT",
                Name: "New Department"),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var response = conflict.Value.Should()
            .BeOfType<AdminDepartmentsController.InactiveDepartmentCodeConflictResponse>()
            .Subject;
        response.Code.Should().Be("inactive_department_code");
        response.Message.Should().Contain("Contact support");
    }

    [Fact]
    public async Task CreateCluster_allows_the_name_of_an_inactive_cluster()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Clusters.Add(new Cluster
        {
            ClusterName = "Former cluster",
            IsActive = false,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var result = await controller.CreateCluster(
            new AdminDepartmentsController.CreateClusterRequest("Former cluster"),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        (await db.Clusters.IgnoreQueryFilters().ToListAsync())
            .Should()
            .ContainSingle(cluster => cluster.ClusterName == "Former cluster" && cluster.IsActive);
    }

    [Fact]
    public async Task CreateCluster_rejects_the_name_of_an_active_cluster()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Clusters.Add(new Cluster
        {
            ClusterName = "Existing cluster",
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var result = await controller.CreateCluster(
            new AdminDepartmentsController.CreateClusterRequest("Existing cluster"),
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be("A cluster with that name already exists.");
    }

    [Fact]
    public async Task UpdateCluster_rejects_the_name_of_another_active_cluster()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = CreateAdmin();
        db.AppUsers.Add(admin);
        db.Clusters.AddRange(
            new Cluster
            {
                ClusterName = "Existing cluster",
            },
            new Cluster
            {
                ClusterName = "Target cluster",
            });
        await db.SaveChangesAsync();
        var targetCluster = await db.Clusters.SingleAsync(
            cluster => cluster.ClusterName == "Target cluster");

        var controller = CreateController(db, admin);

        var result = await controller.UpdateCluster(
            targetCluster.Id,
            new AdminDepartmentsController.UpdateClusterRequest(
                Name: "Existing cluster",
                CaoUserId: null,
                CaoUserIdSet: false),
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be("A cluster with that name already exists.");
    }

    [Fact]
    public async Task UpdateDepartment_to_auto_approve_immediately_approves_each_pending_request_after_confirmation()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = new AppUser
        {
            EntraObjectId = Guid.NewGuid(),
            IamId = "admin",
            FirstLoginUtc = DateTime.UtcNow,
        };
        var requester = new AppUser
        {
            EntraObjectId = Guid.NewGuid(),
            IamId = "faculty",
            Email = "faculty@example.test",
            FirstLoginUtc = DateTime.UtcNow,
        };
        var department = new Department
        {
            DepartmentCode = "DEPT",
            DepartmentName = "Department",
            WorkflowMode = WorkflowMode.ApprovalRequired,
        };
        db.AppUsers.AddRange(admin, requester);
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        db.DepartmentEmailRoutings.Add(new DepartmentEmailRouting
        {
            DepartmentCode = department.DepartmentCode,
            ToEmail = "aggieservice@example.test",
            UpdatedByAppUserId = admin.Id,
        });
        await db.SaveChangesAsync();

        var request = new LeaveRequest
        {
            AppUserId = requester.Id,
            IamId = requester.IamId,
            LeaveTypeId = 1,
            Status = LeaveRequestStatus.PendingApproval,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 1),
            TotalHours = 8,
            ReportingDepartmentCodeSnapshot = department.DepartmentCode,
            ReportingDepartmentNameSnapshot = department.DepartmentName,
            SubmittedAt = DateTime.UtcNow,
            WorkflowModeSnapshot = WorkflowMode.ApprovalRequired,
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = CreateController(db, admin);

        var unconfirmedResult = await controller.UpdateDepartment(
            department.DepartmentCode,
            new AdminDepartmentsController.UpdateDepartmentRequest(
                Name: null,
                ClusterId: null,
                ClusterIdSet: false,
                ApprovalMode: "auto",
                ApprovePendingRequests: false,
                ChairUserId: null,
                ChairUserIdSet: false),
            CancellationToken.None);

        unconfirmedResult.Should().BeOfType<ConflictObjectResult>();
        request.Status.Should().Be(LeaveRequestStatus.PendingApproval);

        var result = await controller.UpdateDepartment(
            department.DepartmentCode,
            new AdminDepartmentsController.UpdateDepartmentRequest(
                Name: null,
                ClusterId: null,
                ClusterIdSet: false,
                ApprovalMode: "auto",
                ApprovePendingRequests: true,
                ChairUserId: null,
                ChairUserIdSet: false),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        department.WorkflowMode.Should().Be(WorkflowMode.DirectSubmission);
        request.Status.Should().Be(LeaveRequestStatus.Approved);
        request.WorkflowModeSnapshot.Should().Be(WorkflowMode.DirectSubmission);
        var action = db.LeaveRequestActions.Should().ContainSingle().Subject;
        action.ActorAppUserId.Should().Be(admin.Id);
        action.ActorIamId.Should().Be(admin.IamId);
        db.OutboundMessages.Should().ContainSingle(message =>
            message.LeaveRequestId == request.Id &&
            message.RecipientEmail == "faculty@example.test" &&
            message.NotificationType == LeaveRequestNotificationTypes.Approved);
        db.OutboundMessages.Should().ContainSingle(message =>
            message.LeaveRequestId == request.Id &&
            message.RecipientEmail == "aggieservice@example.test" &&
            message.NotificationType == LeaveRequestNotificationTypes.Approved);
    }

    private static AdminDepartmentsController CreateController(
        AppDbContext db,
        AppUser admin,
        string defaultRoutingEmail = "aggieservice@example.test")
    {
        var queue = new LeaveRequestNotificationQueue(
            db,
            NullLogger<LeaveRequestNotificationQueue>.Instance);
        return new AdminDepartmentsController(
            db,
            new AdminDirectoryDataService(db),
            new AdminDirectoryService(new AdminDirectoryDataService(db)),
            queue,
            new EmailDeliveryWakeSignal(),
            Options.Create(new DepartmentNotificationOptions
            {
                DefaultRoutingEmail = defaultRoutingEmail,
            }),
            null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, admin.EntraObjectId.ToString()),
                    ], "test")),
                },
            },
        };
    }

    private static AppUser CreateAdmin() => new()
    {
        EntraObjectId = Guid.NewGuid(),
        IamId = "admin",
        FirstLoginUtc = DateTime.UtcNow,
    };
}
