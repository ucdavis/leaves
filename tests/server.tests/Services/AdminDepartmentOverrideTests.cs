using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Server.Controllers;
using Server.Core.Domain;

namespace Server.Tests.Services;

public class AdminDepartmentOverrideTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Closing_override_without_a_faculty_row_uses_effective_dates_and_latest_start_then_id(bool activeDepartment)
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var actor = new AppUser { IamId = "admin", EntraObjectId = Guid.NewGuid() };
        db.AppUsers.Add(actor);
        db.Departments.Add(new Department
        {
            DepartmentCode = "DEPT", DepartmentName = "Department", IsActive = activeDepartment,
        });
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Pacific Standard Time"));
        EmployeeReportingDepartmentOverride Add(int id, int start, int? end = null)
        {
            var item = new EmployeeReportingDepartmentOverride
            {
                Id = id, IamId = "missing", DepartmentCode = "DEPT", CreatedByAppUserId = actor.Id,
                EffectiveStartDate = today.AddDays(start), EffectiveEndDateExclusive = end.HasValue ? today.AddDays(end.Value) : null,
            };
            db.EmployeeReportingDepartmentOverrides.Add(item);
            return item;
        }
        var older = Add(1, -10);
        var tied = Add(2, -1);
        var selected = Add(3, -1);
        var future = Add(4, 1);
        var expired = Add(5, -1, 0);
        await db.SaveChangesAsync();
        var controller = new AdminController(db, null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", actor.EntraObjectId.ToString())], "test")),
                },
            },
        };
        var request = new AdminController.UpdateUserRequest(
            Active: null, Email: null, EmailSet: false, DepartmentOverrideEndDate: null,
            DepartmentOverrideId: null, DepartmentOverrideSet: true, DepartmentOverrideStartDate: null,
            Name: null, NameSet: false);

        var result = await controller.UpsertUserByIamId(" missing ", request, default);

        result.Should().BeOfType<NoContentResult>();
        selected.ClosedByAppUserId.Should().Be(actor.Id);
        selected.ClosedUtc.Should().NotBeNull();
        selected.EffectiveEndDateExclusive.Should().Be(today);
        new[] { older, tied, future, expired }.Should().OnlyContain(item => item.ClosedUtc == null);
        // The endpoint must not need or create a People/AppUser/faculty identity for the owner.
        db.People.Should().BeEmpty();
        db.AppUsers.Should().ContainSingle();
    }
}
