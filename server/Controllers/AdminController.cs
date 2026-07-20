using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;
using Server.Services;

namespace Server.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly AdminDataService _adminDataService;

    public AdminController(AppDbContext db, AdminDataService adminDataService)
    {
        _db = db;
        _adminDataService = adminDataService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        return Ok(await _adminDataService.GetDashboardAsync(cancellationToken));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var iamId = request.IamId.Trim();
        if (string.IsNullOrWhiteSpace(iamId))
        {
            return ValidationProblem("IAM ID is required.");
        }

        var user = new AppUser
        {
            DisplayName = NullIfWhiteSpace(request.Name),
            Email = NullIfWhiteSpace(request.Email),
            EmployeeId = NullIfWhiteSpace(request.EmployeeId),
            EntraObjectId = Guid.NewGuid(),
            FirstLoginUtc = DateTime.UtcNow,
            IamId = iamId,
            IsActive = request.Active,
            LastLoginUtc = null,
            UpdatedUtc = DateTime.UtcNow,
        };

        _db.AppUsers.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateAppUser(ex))
        {
            return Conflict("A user with that IAM ID, employee ID, or identity already exists.");
        }

        return NoContent();
    }

    [HttpPatch("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        if (request.Active.HasValue)
        {
            user.IsActive = request.Active.Value;
        }

        if (request.NameSet)
        {
            user.DisplayName = NullIfWhiteSpace(request.Name);
        }

        if (request.EmailSet)
        {
            user.Email = NullIfWhiteSpace(request.Email);
        }

        user.UpdatedUtc = DateTime.UtcNow;

        if (request.DepartmentOverrideSet)
        {
            var overrideResult = string.IsNullOrWhiteSpace(request.DepartmentOverrideId)
                ? await CloseCurrentDepartmentOverrideAsync(user.IamId.Trim(), cancellationToken)
                : await CreateDepartmentOverrideAsync(user.IamId.Trim(), request, cancellationToken);
            if (overrideResult != null)
            {
                return overrideResult;
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateAppUser(ex))
        {
            return Conflict("A user with that IAM ID, employee ID, or identity already exists.");
        }

        return NoContent();
    }

    [HttpPatch("people/{iamId}")]
    public async Task<IActionResult> UpdatePerson(string iamId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedIamId = iamId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIamId))
        {
            return ValidationProblem("IAM ID is required.");
        }

        var person = await _db.Set<Person>()
            .FirstOrDefaultAsync(item => item.IamId == normalizedIamId, cancellationToken);
        if (person == null)
        {
            return NotFound();
        }

        if (request.NameSet)
        {
            person.FullName = NullIfWhiteSpace(request.Name);
        }

        if (request.EmailSet)
        {
            person.Email = NullIfWhiteSpace(request.Email);
        }

        if (request.DepartmentOverrideSet)
        {
            var overrideResult = string.IsNullOrWhiteSpace(request.DepartmentOverrideId)
                ? await CloseCurrentDepartmentOverrideAsync(normalizedIamId, cancellationToken)
                : await CreateDepartmentOverrideAsync(normalizedIamId, request, cancellationToken);
            if (overrideResult != null)
            {
                return overrideResult;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new AdminStatusResponse("Admin access granted."));
    }

    private static bool IsDuplicateAppUser(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private async Task<IActionResult?> CreateDepartmentOverrideAsync(
        string iamId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var departmentCode = request.DepartmentOverrideId?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return null;
        }

        var departmentExists = await _db.Departments.AnyAsync(
            department => department.DepartmentCode == departmentCode,
            cancellationToken);
        if (!departmentExists)
        {
            return ValidationProblem("Selected department does not exist.");
        }

        if (!DateOnly.TryParse(request.DepartmentOverrideStartDate, out var startDate))
        {
            return ValidationProblem("Department override start date is required.");
        }

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(request.DepartmentOverrideEndDate))
        {
            if (!DateOnly.TryParse(request.DepartmentOverrideEndDate, out var parsedEndDate))
            {
                return ValidationProblem("Department override end date is invalid.");
            }

            if (parsedEndDate <= startDate)
            {
                return ValidationProblem("Department override end date must be after the start date.");
            }

            endDate = parsedEndDate;
        }

        var createdByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (createdByAppUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before department overrides can be updated.");
        }

        _db.EmployeeReportingDepartmentOverrides.Add(new EmployeeReportingDepartmentOverride
        {
            CreatedByAppUserId = createdByAppUserId.Value,
            CreatedUtc = DateTime.UtcNow,
            DepartmentCode = departmentCode,
            EffectiveEndDateExclusive = endDate,
            EffectiveStartDate = startDate,
            IamId = iamId,
            Reason = "Admin people edit",
        });

        return null;
    }

    private async Task<IActionResult?> CloseCurrentDepartmentOverrideAsync(
        string iamId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentOverride = await _db.EmployeeReportingDepartmentOverrides
            .Where(item => item.IamId == iamId &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentOverride == null)
        {
            return null;
        }

        var closedByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (closedByAppUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before department overrides can be updated.");
        }

        currentOverride.ClosedByAppUserId = closedByAppUserId.Value;
        currentOverride.ClosedUtc = DateTime.UtcNow;
        currentOverride.EffectiveEndDateExclusive = today;
        return null;
    }

    private async Task<int?> GetAuthenticatedAppUserId(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId) || !Guid.TryParse(userId, out var entraObjectId))
        {
            return null;
        }

        return await _db.AppUsers
            .AsNoTracking()
            .Where(user => user.EntraObjectId == entraObjectId)
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private sealed record AdminStatusResponse(string Message);
    public sealed record CreateUserRequest(bool Active, string? Email, string? EmployeeId, string IamId, string? Name);
    public sealed record UpdateUserRequest(
        bool? Active,
        string? Email,
        bool EmailSet,
        string? DepartmentOverrideEndDate,
        string? DepartmentOverrideId,
        bool DepartmentOverrideSet,
        string? DepartmentOverrideStartDate,
        string? Name,
        bool NameSet);
}
