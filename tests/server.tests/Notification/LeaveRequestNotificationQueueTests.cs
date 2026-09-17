using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Domain;
using Server.Core.Notification;

namespace Server.Tests.Notification;

public sealed class LeaveRequestNotificationQueueTests
{
    [Fact]
    public async Task QueueSubmissionAsync_queues_requester_confirmation_for_a_direct_submission()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var requester = CreateUser("requester@example.test");
        db.AppUsers.Add(requester);
        await db.SaveChangesAsync();

        var request = CreateRequest(requester.Id, WorkflowMode.DirectSubmission);
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var queue = CreateQueue(db);
        await queue.QueueSubmissionAsync(request, requester, CancellationToken.None);
        await db.SaveChangesAsync();

        db.OutboundMessages.Should().ContainSingle(message =>
            message.RecipientEmail == "requester@example.test" &&
            message.NotificationType == LeaveRequestNotificationTypes.AutoApproved &&
            message.Status == OutboundMessageStatus.Pending);
    }

    [Fact]
    public async Task QueueSubmissionAsync_queues_each_department_routing_recipient_for_approval()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var requester = CreateUser("requester@example.test");
        db.AppUsers.Add(requester);
        db.Departments.Add(new Department
        {
            DepartmentCode = "123456",
            DepartmentName = "Example Department",
            WorkflowMode = WorkflowMode.ApprovalRequired,
        });
        await db.SaveChangesAsync();
        db.DepartmentEmailRoutings.AddRange(
            new DepartmentEmailRouting
            {
                DepartmentCode = "123456",
                ToEmail = "chair@example.test",
                UpdatedByAppUserId = requester.Id,
            },
            new DepartmentEmailRouting
            {
                DepartmentCode = "123456",
                ToEmail = "leave-admin@example.test",
                UpdatedByAppUserId = requester.Id,
            });
        await db.SaveChangesAsync();

        var request = CreateRequest(requester.Id, WorkflowMode.ApprovalRequired);
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var queue = CreateQueue(db);
        await queue.QueueSubmissionAsync(request, requester, CancellationToken.None);
        await db.SaveChangesAsync();

        db.OutboundMessages.Should().HaveCount(2).And.OnlyContain(message =>
            message.NotificationType == LeaveRequestNotificationTypes.SubmittedForApproval &&
            message.Status == OutboundMessageStatus.Pending);
    }

    [Fact]
    public async Task QueueDecision_queues_a_requester_decision_once()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var requester = CreateUser("requester@example.test");
        db.AppUsers.Add(requester);
        await db.SaveChangesAsync();

        var request = CreateRequest(requester.Id, WorkflowMode.ApprovalRequired);
        request.Status = LeaveRequestStatus.Approved;
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        var queue = CreateQueue(db);
        queue.QueueDecision(request, requester);
        queue.QueueDecision(request, requester);
        await db.SaveChangesAsync();

        db.OutboundMessages.Should().ContainSingle(message =>
            message.NotificationType == LeaveRequestNotificationTypes.Approved &&
            message.RecipientEmail == "requester@example.test");
    }

    private static LeaveRequestNotificationQueue CreateQueue(Server.Core.Data.AppDbContext db) =>
        new(db, NullLogger<LeaveRequestNotificationQueue>.Instance);

    private static AppUser CreateUser(string email) => new()
    {
        EntraObjectId = Guid.NewGuid(),
        IamId = $"iam{Guid.NewGuid():N}"[..10],
        Email = email,
        DisplayName = "Requesting Faculty",
        FirstLoginUtc = DateTime.UtcNow,
    };

    private static LeaveRequest CreateRequest(int appUserId, WorkflowMode workflowMode) => new()
    {
        AppUserId = appUserId,
        IamId = "iam1234567",
        LeaveTypeId = 1,
        Status = workflowMode == WorkflowMode.ApprovalRequired
            ? LeaveRequestStatus.PendingApproval
            : LeaveRequestStatus.Approved,
        StartDate = new DateOnly(2026, 9, 14),
        EndDate = new DateOnly(2026, 9, 15),
        TotalHours = 16,
        ReportingDepartmentCodeSnapshot = "123456",
        ReportingDepartmentNameSnapshot = "Example Department",
        WorkflowModeSnapshot = workflowMode,
        SubmittedAt = DateTime.UtcNow,
    };
}
