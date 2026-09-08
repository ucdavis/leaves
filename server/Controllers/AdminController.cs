using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
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

    [HttpGet("/Admin/Emulate/{identifier}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Emulate([FromRoute] string? identifier, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = NullIfWhiteSpace(identifier)?.ToLowerInvariant();
        if (normalizedIdentifier == null)
        {
            return Content("A user identifier is required.");
        }

        if (User.HasClaim(claim => claim.Type == AuthenticationHelper.EmulatingUserClaimType))
        {
            return Content("You are already emulating a user.");
        }

        var people = await _db.People
            .Where(person => person.IamId.Trim().ToLower() == normalizedIdentifier ||
                (person.EmployeeId != null && person.EmployeeId.Trim().ToLower() == normalizedIdentifier) ||
                (person.Email != null && person.Email.Trim().ToLower() == normalizedIdentifier) ||
                (person.UserId != null && person.UserId.Trim().ToLower() == normalizedIdentifier))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (people.Count == 0)
        {
            return Content("User not found in the People table.");
        }

        if (people.Count > 1)
        {
            return Content("Multiple people match that identifier. Use the user's IAM ID.");
        }

        var person = people[0];
        var iamId = person.IamId.Trim();
        var matchingUsers = _db.AppUsers
            .Where(user => user.IamId.Trim() == iamId)
            .OrderByDescending(user => user.UpdatedUtc)
            .ThenByDescending(user => user.Id);
        var user = await matchingUsers.FirstOrDefaultAsync(cancellationToken);
        if (user == null)
        {
            var now = DateTime.UtcNow;
            user = new AppUser
            {
                EntraObjectId = Guid.NewGuid(),
                IamId = iamId,
                EmployeeId = NullIfWhiteSpace(person.EmployeeId),
                DisplayName = NullIfWhiteSpace(person.FullName) ?? NullIfWhiteSpace(person.UserId)
                    ?? NullIfWhiteSpace(person.Email) ?? iamId,
                Email = NullIfWhiteSpace(person.Email),
                FirstLoginUtc = now,
                CreatedUtc = now,
                UpdatedUtc = now,
            };
            _db.AppUsers.Add(user);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateAppUser(ex))
            {
                _db.Entry(user).State = EntityState.Detached;
                user = await matchingUsers.FirstOrDefaultAsync(cancellationToken);
                if (user == null)
                {
                    return Conflict("An AppUser with that employee ID or identity already exists for another person.");
                }
            }
        }

        var userId = user.EntraObjectId.ToString();
        var displayName = NullIfWhiteSpace(user.DisplayName) ?? NullIfWhiteSpace(person.FullName)
            ?? NullIfWhiteSpace(person.UserId) ?? iamId;
        var claims = new List<Claim>
        {
            new(ClaimConstants.ObjectId, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new("name", displayName),
            new("ucdPersonIAMID", user.IamId.Trim()),
            new(AuthenticationHelper.EmulatingUserClaimType, User.GetUserId()),
        };
        var email = NullIfWhiteSpace(person.Email) ?? NullIfWhiteSpace(user.Email);
        if (email != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
            claims.Add(new Claim("preferred_username", email));
        }

        var roles = await _userService.GetRolesForUser(userId);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return LocalRedirect("/");
    }

    [HttpPost("users")]
    public async Task<IActionResult> UpdateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var iamId = request.IamId.Trim();
        if (string.IsNullOrWhiteSpace(iamId))
        {
            return ValidationProblem("IAM ID is required.");
        }

        var user = await _db.AppUsers.FirstOrDefaultAsync(item => item.IamId == iamId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        if (request.Name != null)
        {
            user.DisplayName = NullIfWhiteSpace(request.Name);
        }

        if (request.Email != null)
        {
            user.Email = NullIfWhiteSpace(request.Email);
        }

        if (request.EmployeeId != null)
        {
            user.EmployeeId = NullIfWhiteSpace(request.EmployeeId);
        }

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
        if (user == null && !request.DepartmentOverrideSet)
        {
            return NotFound();
        }

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
