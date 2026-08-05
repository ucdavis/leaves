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
    private readonly AdminDirectoryService _adminDirectoryService;
    private readonly AdminStatusService _adminStatusService;
    private readonly IUserService _userService;

    public AdminController(
        AppDbContext db,
        AdminDirectoryService adminDirectoryService,
        AdminStatusService adminStatusService,
        IUserService userService)
    {
        _db = db;
        _adminDirectoryService = adminDirectoryService;
        _adminStatusService = adminStatusService;
        _userService = userService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _adminStatusService.GetStatusAsync(cancellationToken));
    }

    [HttpGet("faculty")]
    public async Task<IActionResult> GetFaculty(CancellationToken cancellationToken)
    {
        return Ok(await _adminDirectoryService.GetFacultyAsync(cancellationToken));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var iamId = request.IamId.Trim();
        if (string.IsNullOrWhiteSpace(iamId))
        {
            return ValidationProblem("IAM ID is required.");
        }

        var user = await _db.AppUsers.FirstOrDefaultAsync(item => item.IamId == iamId, cancellationToken);
        if (user == null)
        {
            return NoContent();
        }

        user.DisplayName = NullIfWhiteSpace(request.Name);
        user.Email = NullIfWhiteSpace(request.Email);
        user.EmployeeId = NullIfWhiteSpace(request.EmployeeId);
        user.IsActive = request.Active;
        user.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
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

    [HttpPatch("users/by-iam/{iamId}")]
    public async Task<IActionResult> UpsertUserByIamId(string iamId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedIamId = iamId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIamId))
        {
            return ValidationProblem("IAM ID is required.");
        }

        var person = await _db.Set<Person>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IamId == normalizedIamId, cancellationToken);
        var user = await _db.AppUsers.FirstOrDefaultAsync(item => item.IamId == normalizedIamId, cancellationToken);
        var shouldSave = false;

        if (user != null && request.NameSet)
        {
            user.DisplayName = NullIfWhiteSpace(request.Name) ?? NullIfWhiteSpace(person?.FullName);
            shouldSave = true;
        }

        if (user != null && request.EmailSet)
        {
            user.Email = NullIfWhiteSpace(request.Email) ?? NullIfWhiteSpace(person?.Email);
            shouldSave = true;
        }

        if (user != null && request.Active.HasValue)
        {
            user.IsActive = request.Active.Value;
            shouldSave = true;
        }

        if (user != null && !request.NameSet && user.DisplayName == null)
        {
            user.DisplayName = NullIfWhiteSpace(person?.FullName);
            shouldSave = true;
        }

        if (user != null && !request.EmailSet && user.Email == null)
        {
            user.Email = NullIfWhiteSpace(person?.Email);
            shouldSave = true;
        }

        if (user != null && user.EmployeeId == null)
        {
            user.EmployeeId = NullIfWhiteSpace(person?.EmployeeId);
            shouldSave = true;
        }

        if (user != null)
        {
            user.UpdatedUtc = DateTime.UtcNow;
            shouldSave = true;
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

            shouldSave = true;
        }

        if (shouldSave)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
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

        var department = await _db.Departments.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode,
            cancellationToken);
        if (department == null || !department.IsActive)
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
        var currentOverrideId = await _db.CurrentEmployees
            .Where(employee => employee.IamId == iamId)
            .Select(employee => employee.ReportingDepartmentOverrideId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!currentOverrideId.HasValue)
        {
            return null;
        }

        var currentOverride = await _db.EmployeeReportingDepartmentOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == currentOverrideId.Value, cancellationToken);

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
        currentOverride.EffectiveEndDateExclusive = DateOnly.FromDateTime(DateTime.UtcNow);
        return null;
    }

    private async Task<int?> GetAuthenticatedAppUserId(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId) || !Guid.TryParse(userId, out var entraObjectId))
        {
            return null;
        }

        var appUserId = await _db.AppUsers
            .AsNoTracking()
            .Where(user => user.EntraObjectId == entraObjectId)
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (appUserId != null)
        {
            return appUserId;
        }

        await _userService.EnsureUserProfileAsync(
            User,
            recordSignIn: false,
            cancellationToken: cancellationToken);

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
