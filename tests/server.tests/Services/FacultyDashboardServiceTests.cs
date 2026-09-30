using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Core.Data;
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

    [Fact]
    public async Task Submission_rejects_more_than_24_hours_per_selected_leave_day()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var user = new AppUser { IamId = "1234567890", EntraObjectId = Guid.NewGuid() };
        var leaveType = new LeaveType { LeaveTypeKey = "Vacation", DisplayName = "Vacation" };
        db.AppUsers.Add(user);
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        var date = new DateOnly(2026, 10, 1);

        var result = await CreateService(db).CreateLeaveRequestAsync(
            CreatePrincipal(user),
            new CreateFacultyLeaveRequest(leaveType.Id, null, date, date, 25, null, null),
            default);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Key.Should().Be("totalHours");
        result.Errors["totalHours"].Should().Equal("Hours must be 24 or fewer per leave day.");
    }

    [Fact]
    public async Task Submission_allows_more_than_240_total_hours_when_each_selected_day_is_at_most_24_hours()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var user = new AppUser { IamId = "1234567890", EntraObjectId = Guid.NewGuid() };
        var leaveType = new LeaveType { LeaveTypeKey = "Vacation", DisplayName = "Vacation" };
        db.AppUsers.Add(user);
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateLeaveRequestAsync(
            CreatePrincipal(user),
            new CreateFacultyLeaveRequest(
                leaveType.Id,
                null,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 31),
                744,
                null,
                null),
            default);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Key.Should().Be("faculty");
    }

    [Fact]
    public async Task Overlap_check_uses_recorded_leave_days_and_falls_back_for_legacy_requests()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var selectedDate = new DateOnly(2026, 10, 3);
        var requestWithDays = new LeaveRequest
        {
            IamId = "faculty",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 5),
            Status = LeaveRequestStatus.Approved,
        };
        requestWithDays.Days.Add(new LeaveRequestDay
        {
            LeaveDate = new DateOnly(2026, 10, 2),
            Hours = 8,
        });
        db.LeaveRequests.Add(requestWithDays);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        (await service.HasActiveOverlappingLeaveRequestAsync(
            "faculty", [selectedDate], CancellationToken.None)).Should().BeFalse();

        db.LeaveRequests.Add(new LeaveRequest
        {
            IamId = "faculty",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 5),
            Status = LeaveRequestStatus.Approved,
        });
        await db.SaveChangesAsync();

        (await service.HasActiveOverlappingLeaveRequestAsync(
            "faculty", [selectedDate], CancellationToken.None)).Should().BeTrue();
    }

    private static FacultyDashboardService CreateService(AppDbContext db) => new(
        db,
        new LeaveRequestNotificationQueue(db, NullLogger<LeaveRequestNotificationQueue>.Instance),
        new EmailDeliveryWakeSignal(),
        NullLogger<FacultyDashboardService>.Instance);

    private static ClaimsPrincipal CreatePrincipal(AppUser user) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.EntraObjectId.ToString()),
        new Claim(ClaimTypes.Role, "Faculty"),
    ], "Test"));
}
