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
[Route("api/admin/roles")]
public sealed class AdminRolesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly AdminRolesService _adminRolesService;
    private readonly IUserService _userService;

    public AdminRolesController(AppDbContext db, AdminRolesService adminRolesService, IUserService userService)
    {
        _db = db;
        _adminRolesService = adminRolesService;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRolesAsync(CancellationToken cancellationToken)
    {
        return Ok(await _adminRolesService.GetRolesAsync(cancellationToken));
    }

    [HttpPost("admins")]
    public async Task<IActionResult> AddAdminAsync([FromBody] AddAdminRequest request, CancellationToken cancellationToken)
    {
        var iamId = request.IamId?.Trim();
        if (string.IsNullOrWhiteSpace(iamId))
        {
            return ValidationProblem("IAM ID is required.");
        }

        var createdByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (createdByAppUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before role assignments can be updated.");
        }

        _db.AppAdminAssignments.Add(new AppAdminAssignment
        {
            CreatedByAppUserId = createdByAppUserId.Value,
            CreatedUtc = DateTime.UtcNow,
            IamId = iamId,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            return Conflict("That user is already an application admin.");
        }

        return NoContent();
    }

    [HttpPost("caos")]
    public async Task<IActionResult> AddCaoAsync([FromBody] AddCaoRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateImmediateAssignmentAsync(
            iamId: request.IamId,
            cancellationToken: cancellationToken);
        if (validationResult.Error != null)
        {
            return validationResult.Error;
        }

        var cluster = await _db.Clusters.FirstOrDefaultAsync(
            item => item.Id == request.ClusterId,
            cancellationToken);
        if (cluster == null || !cluster.IsActive)
        {
            return ValidationProblem("Selected cluster does not exist.");
        }

        var hasActiveAssignment = await HasActiveClusterCaoAssignmentAsync(
            request.ClusterId,
            validationResult.IamId!,
            validationResult.StartDate!.Value,
            cancellationToken);
        if (hasActiveAssignment)
        {
            return Conflict("That user already has an active CAO assignment for this cluster.");
        }

        _db.ClusterCaoAssignments.Add(new ClusterCaoAssignment
        {
            ClusterId = request.ClusterId,
            CreatedByAppUserId = validationResult.CreatedByAppUserId!.Value,
            CreatedUtc = DateTime.UtcNow,
            EffectiveStartDate = validationResult.StartDate!.Value,
            IamId = validationResult.IamId!,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("chairs")]
    public async Task<IActionResult> AddChairAsync([FromBody] AddChairRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateImmediateAssignmentAsync(
            iamId: request.IamId,
            cancellationToken: cancellationToken);
        if (validationResult.Error != null)
        {
            return validationResult.Error;
        }

        var departmentCode = request.DepartmentCode?.Trim().ToUpperInvariant();
        var department = await _db.Departments.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(departmentCode) || department == null || !department.IsActive)
        {
            return ValidationProblem("Selected department does not exist.");
        }

        var personDepartmentCodes = await GetCurrentDepartmentCodesAsync(validationResult.IamId!, cancellationToken);
        if (!personDepartmentCodes.Contains(departmentCode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationProblem("Selected person can only be assigned as department chair for one of their current departments.");
        }

        var hasActiveAssignment = await HasActiveDepartmentChairAssignmentAsync(
            departmentCode,
            validationResult.IamId!,
            validationResult.StartDate!.Value,
            cancellationToken);
        if (hasActiveAssignment)
        {
            return Conflict("That user already has an active department chair assignment for this department.");
        }

        _db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            CreatedByAppUserId = validationResult.CreatedByAppUserId!.Value,
            CreatedUtc = DateTime.UtcNow,
            DepartmentCode = departmentCode,
            EffectiveStartDate = validationResult.StartDate!.Value,
            IamId = validationResult.IamId!,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("admins/{id:int}")]
    public async Task<IActionResult> RemoveAdminAsync(int id, CancellationToken cancellationToken)
    {
        var assignment = await _db.AppAdminAssignments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment == null)
        {
            return NotFound();
        }

        _db.AppAdminAssignments.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("caos/{id:int}")]
    public async Task<IActionResult> RemoveCaoAsync(int id, CancellationToken cancellationToken)
    {
        var assignment = await _db.ClusterCaoAssignments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment == null)
        {
            return NotFound();
        }

        return await CloseDatedAssignmentAsync(assignment, cancellationToken);
    }

    [HttpDelete("chairs/{id:int}")]
    public async Task<IActionResult> RemoveChairAsync(int id, CancellationToken cancellationToken)
    {
        var assignment = await _db.DepartmentChairAssignments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment == null)
        {
            return NotFound();
        }

        return await CloseDatedAssignmentAsync(assignment, cancellationToken);
    }

    private async Task<IActionResult> CloseDatedAssignmentAsync(ClusterCaoAssignment assignment, CancellationToken cancellationToken)
    {
        var closedByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (closedByAppUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before role assignments can be updated.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        assignment.ClosedByAppUserId = closedByAppUserId.Value;
        assignment.ClosedUtc = DateTime.UtcNow;
        if (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today)
        {
            assignment.EffectiveEndDateExclusive = today;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> CloseDatedAssignmentAsync(DepartmentChairAssignment assignment, CancellationToken cancellationToken)
    {
        var closedByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (closedByAppUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before role assignments can be updated.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        assignment.ClosedByAppUserId = closedByAppUserId.Value;
        assignment.ClosedUtc = DateTime.UtcNow;
        if (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today)
        {
            assignment.EffectiveEndDateExclusive = today;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ImmediateAssignmentValidationResult> ValidateImmediateAssignmentAsync(
        string? iamId,
        CancellationToken cancellationToken)
    {
        var trimmedIamId = iamId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedIamId))
        {
            return ImmediateAssignmentValidationResult.WithError(ValidationProblem("IAM ID is required."));
        }

        var createdByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (createdByAppUserId == null)
        {
            return ImmediateAssignmentValidationResult.WithError(ValidationProblem("The authenticated admin must have an AppUser row before role assignments can be updated."));
        }

        return new ImmediateAssignmentValidationResult(
            trimmedIamId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            createdByAppUserId,
            null);
    }

    private Task<bool> HasActiveClusterCaoAssignmentAsync(
        int clusterId,
        string iamId,
        DateOnly onDate,
        CancellationToken cancellationToken,
        int? excludeAssignmentId = null)
    {
        return _db.ClusterCaoAssignments.AnyAsync(
            assignment => assignment.ClusterId == clusterId &&
                          assignment.ClosedUtc == null &&
                          assignment.IamId == iamId &&
                          assignment.EffectiveStartDate <= onDate &&
                          (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > onDate) &&
                          (!excludeAssignmentId.HasValue || assignment.Id != excludeAssignmentId.Value),
            cancellationToken);
    }

    private Task<bool> HasActiveDepartmentChairAssignmentAsync(
        string departmentCode,
        string iamId,
        DateOnly onDate,
        CancellationToken cancellationToken,
        int? excludeAssignmentId = null)
    {
        return _db.DepartmentChairAssignments.AnyAsync(
            assignment => assignment.DepartmentCode == departmentCode &&
                          assignment.ClosedUtc == null &&
                          assignment.IamId == iamId &&
                          assignment.EffectiveStartDate <= onDate &&
                          (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > onDate) &&
                          (!excludeAssignmentId.HasValue || assignment.Id != excludeAssignmentId.Value),
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetCurrentDepartmentCodesAsync(
        string iamId,
        CancellationToken cancellationToken)
    {
        var departmentCode = await _db.CurrentEmployees
            .Where(employee => employee.IamId == iamId)
            .Select(employee => employee.ResolvedReportingDepartmentCode)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(departmentCode)
            ? []
            : [departmentCode.Trim()];
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

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    public sealed record AddAdminRequest(string? IamId);
    public sealed record AddCaoRequest(int ClusterId, string? IamId);
    public sealed record AddChairRequest(string? DepartmentCode, string? IamId);
    private sealed record ImmediateAssignmentValidationResult(
        string? IamId,
        DateOnly? StartDate,
        int? CreatedByAppUserId,
        IActionResult? Error)
    {
        public static ImmediateAssignmentValidationResult WithError(IActionResult error)
        {
            return new ImmediateAssignmentValidationResult(null, null, null, error);
        }
    }
}
