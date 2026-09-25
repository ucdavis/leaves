using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Domain;
using Server.Core.Notification;
using Server.Services;

namespace Server.Tests.Services;

public class FacultyDashboardServiceTests
{
    [Fact]
    public async Task Submission_without_current_faculty_reports_eligibility_and_creates_no_request()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var user = new AppUser { IamId = "1234567890", EntraObjectId = Guid.NewGuid() };
        var leaveType = new LeaveType { LeaveTypeKey = "Vacation", DisplayName = "Vacation" };
        db.AppUsers.Add(user);
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.EntraObjectId.ToString()!),
            new Claim(ClaimTypes.Role, "Faculty"),
        ], "Test"));
        var service = new FacultyDashboardService(
            db,
            new LeaveRequestNotificationQueue(db, NullLogger<LeaveRequestNotificationQueue>.Instance),
            new EmailDeliveryWakeSignal(),
            NullLogger<FacultyDashboardService>.Instance);
        var date = new DateOnly(2026, 10, 1);

        var result = await service.CreateLeaveRequestAsync(principal,
            new CreateFacultyLeaveRequest(leaveType.Id, null, date, date, 8, null, null), default);

        result.Succeeded.Should().BeFalse();
        result.MissingUser.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Key.Should().Be("faculty");
        result.Errors["faculty"].Should().Equal("Only current faculty with accrual records can submit leave requests.");
        db.LeaveRequests.Should().BeEmpty();
    }
}
