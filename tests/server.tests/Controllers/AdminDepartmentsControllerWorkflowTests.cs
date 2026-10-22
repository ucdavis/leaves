using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Controllers;
using Server.Core.Domain;
using Server.Core.Notification;
using Server.Services;

namespace Server.Tests.Controllers;

public class AdminDepartmentsControllerWorkflowTests
{
    [Fact]
    public async Task CreateDepartment_without_a_cluster_returns_a_validation_error()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var controller = new AdminDepartmentsController(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var result = await controller.CreateDepartment(
            new AdminDepartmentsController.CreateDepartmentRequest(
                ApprovalMode: "auto",
                ClusterId: null,
                Code: "DEPT",
                Name: "Department"),
            CancellationToken.None);

        var validationProblem = result.Should().BeOfType<ObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        validationProblem.Detail.Should().Be("Cluster is required.");
    }

    [Fact]
    public async Task ReactivateDepartment_with_a_cluster_assigns_the_department_to_that_cluster()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var admin = new AppUser
        {
            EntraObjectId = Guid.NewGuid(),
            IamId = "admin",
            FirstLoginUtc = DateTime.UtcNow,
        };
        db.Clusters.Add(new Cluster { Id = 1, ClusterName = "Cluster" });
        db.Departments.Add(new Department
        {
            ClusterId = null,
            DepartmentCode = "DEPT",
            DepartmentName = "Department",
            IsActive = false,
            WorkflowMode = WorkflowMode.ApprovalRequired,
        });
        db.AppUsers.Add(admin);
        await db.SaveChangesAsync();
        db.DepartmentEmailRoutings.Add(new DepartmentEmailRouting
        {
            DepartmentCode = "DEPT",
            ToEmail = "routing@example.test",
            UpdatedByAppUserId = admin.Id,
        });
        await db.SaveChangesAsync();

        var controller = new AdminDepartmentsController(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var result = await controller.ReactivateDepartment(
            "DEPT",
            new AdminDepartmentsController.ReactivateDepartmentRequest(1),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        var department = await db.Departments.IgnoreQueryFilters()
            .SingleAsync(item => item.DepartmentCode == "DEPT");
        department.IsActive.Should().BeTrue();
        department.ClusterId.Should().Be(1);
        department.WorkflowMode.Should().Be(WorkflowMode.ApprovalRequired);
        db.DepartmentEmailRoutings.Should().ContainSingle(routing =>
            routing.DepartmentCode == "DEPT" &&
            routing.ToEmail == "routing@example.test");
    }

    [Fact]
    public async Task ReactivateDepartment_without_a_cluster_returns_a_validation_error()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.Departments.Add(new Department
        {
            DepartmentCode = "DEPT",
            DepartmentName = "Department",
            IsActive = false,
        });
        await db.SaveChangesAsync();

        var controller = new AdminDepartmentsController(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var result = await controller.ReactivateDepartment(
            "DEPT",
            null,
            CancellationToken.None);

        var validationProblem = result.Should().BeOfType<ObjectResult>().Which.Value
            .Should().BeOfType<ValidationProblemDetails>().Subject;
        validationProblem.Detail.Should().Be("Cluster is required.");
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

        var queue = new LeaveRequestNotificationQueue(
            db,
            NullLogger<LeaveRequestNotificationQueue>.Instance);
        var controller = new AdminDepartmentsController(
            db,
            new AdminDirectoryDataService(db),
            new AdminDirectoryService(new AdminDirectoryDataService(db)),
            queue,
            new EmailDeliveryWakeSignal(),
            null!,
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
            message.NotificationType == LeaveRequestNotificationTypes.Approved);
        db.OutboundMessages.Should().ContainSingle(message =>
            message.LeaveRequestId == request.Id &&
            message.RecipientEmail == "aggieservice@example.test" &&
            message.NotificationType == LeaveRequestNotificationTypes.FinalizedForRouting);
    }
}
