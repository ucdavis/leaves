using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

namespace Server.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/admin/roles")]
public sealed class AdminRolesController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public AdminRolesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetRolesAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var users = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);
        var usersByIamId = users.ToDictionary(user => user.IamId.Trim(), StringComparer.OrdinalIgnoreCase);

        var departments = await _db.Departments
            .AsNoTracking()
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);
        var departmentsByCode = departments.ToDictionary(department => department.DepartmentCode, StringComparer.OrdinalIgnoreCase);

        var clusters = await _db.Clusters
            .AsNoTracking()
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);
        var clustersById = clusters.ToDictionary(cluster => cluster.Id);

        var adminAssignments = await _db.AppAdminAssignments
            .AsNoTracking()
            .OrderBy(assignment => assignment.IamId)
            .ToListAsync(cancellationToken);
        var caoAssignments = await _db.ClusterCaoAssignments
            .AsNoTracking()
            .OrderBy(assignment => assignment.ClusterId)
            .ThenBy(assignment => assignment.IamId)
            .ToListAsync(cancellationToken);
        var chairAssignments = await _db.DepartmentChairAssignments
            .AsNoTracking()
            .OrderBy(assignment => assignment.DepartmentCode)
            .ThenBy(assignment => assignment.IamId)
            .ToListAsync(cancellationToken);

        var assignments = adminAssignments
            .Select(assignment => CreateAssignmentResponse(
                active: true,
                effectiveEndDate: null,
                effectiveStartDate: null,
                id: assignment.Id.ToString(),
                iamId: assignment.IamId,
                targetId: null,
                targetName: null,
                type: "admin",
                usersByIamId: usersByIamId))
            .Concat(caoAssignments.Select(assignment =>
            {
                clustersById.TryGetValue(assignment.ClusterId, out var cluster);
                return CreateAssignmentResponse(
                    active: IsActive(assignment.EffectiveStartDate, assignment.EffectiveEndDateExclusive, today, assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: assignment.ClusterId.ToString(),
                    targetName: cluster?.ClusterName ?? $"Cluster {assignment.ClusterId}",
                    type: "cao",
                    usersByIamId: usersByIamId);
            }))
            .Concat(chairAssignments.Select(assignment =>
            {
                departmentsByCode.TryGetValue(assignment.DepartmentCode, out var department);
                return CreateAssignmentResponse(
                    active: IsActive(assignment.EffectiveStartDate, assignment.EffectiveEndDateExclusive, today, assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: assignment.DepartmentCode,
                    targetName: department?.DepartmentName ?? assignment.DepartmentCode,
                    type: "chair",
                    usersByIamId: usersByIamId);
            }))
            .OrderByDescending(assignment => assignment.Active)
            .ThenBy(assignment => assignment.Type)
            .ThenBy(assignment => assignment.TargetName)
            .ThenBy(assignment => assignment.Name)
            .ToList();

        return Ok(new AdminRolesResponse(
            Assignments: assignments,
            Clusters: clusters.Select(cluster => new AdminRoleOption(cluster.Id.ToString(), cluster.ClusterName)).ToList(),
            Departments: departments.Select(department => new AdminRoleOption(department.DepartmentCode, department.DepartmentName)).ToList(),
            Users: users.Select(user => new AdminRoleUserOption(
                Email: user.Email ?? string.Empty,
                IamId: user.IamId.Trim(),
                Name: user.DisplayName ?? user.IamId.Trim())).ToList()));
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
        var validationResult = await ValidateDatedAssignmentAsync(
            iamId: request.IamId,
            effectiveStartDate: request.EffectiveStartDate,
            effectiveEndDate: request.EffectiveEndDate,
            cancellationToken: cancellationToken);
        if (validationResult.Error != null)
        {
            return validationResult.Error;
        }

        var clusterExists = await _db.Clusters.AnyAsync(cluster => cluster.Id == request.ClusterId, cancellationToken);
        if (!clusterExists)
        {
            return ValidationProblem("Selected cluster does not exist.");
        }

        _db.ClusterCaoAssignments.Add(new ClusterCaoAssignment
        {
            ClusterId = request.ClusterId,
            CreatedByAppUserId = validationResult.CreatedByAppUserId!.Value,
            CreatedUtc = DateTime.UtcNow,
            EffectiveEndDateExclusive = validationResult.EndDate,
            EffectiveStartDate = validationResult.StartDate!.Value,
            IamId = validationResult.IamId!,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("chairs")]
    public async Task<IActionResult> AddChairAsync([FromBody] AddChairRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await ValidateDatedAssignmentAsync(
            iamId: request.IamId,
            effectiveStartDate: request.EffectiveStartDate,
            effectiveEndDate: request.EffectiveEndDate,
            cancellationToken: cancellationToken);
        if (validationResult.Error != null)
        {
            return validationResult.Error;
        }

        var departmentCode = request.DepartmentCode?.Trim().ToUpperInvariant();
        var departmentExists = await _db.Departments.AnyAsync(department => department.DepartmentCode == departmentCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(departmentCode) || !departmentExists)
        {
            return ValidationProblem("Selected department does not exist.");
        }

        _db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            CreatedByAppUserId = validationResult.CreatedByAppUserId!.Value,
            CreatedUtc = DateTime.UtcNow,
            DepartmentCode = departmentCode,
            EffectiveEndDateExclusive = validationResult.EndDate,
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

    private async Task<DatedAssignmentValidationResult> ValidateDatedAssignmentAsync(
        string? iamId,
        string? effectiveStartDate,
        string? effectiveEndDate,
        CancellationToken cancellationToken)
    {
        var trimmedIamId = iamId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedIamId))
        {
            return DatedAssignmentValidationResult.WithError(ValidationProblem("IAM ID is required."));
        }

        if (!DateOnly.TryParse(effectiveStartDate, out var startDate))
        {
            return DatedAssignmentValidationResult.WithError(ValidationProblem("Start date is required."));
        }

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(effectiveEndDate))
        {
            if (!DateOnly.TryParse(effectiveEndDate, out var parsedEndDate))
            {
                return DatedAssignmentValidationResult.WithError(ValidationProblem("End date is invalid."));
            }

            if (parsedEndDate <= startDate)
            {
                return DatedAssignmentValidationResult.WithError(ValidationProblem("End date must be after the start date."));
            }

            endDate = parsedEndDate;
        }

        var createdByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (createdByAppUserId == null)
        {
            return DatedAssignmentValidationResult.WithError(ValidationProblem("The authenticated admin must have an AppUser row before role assignments can be updated."));
        }

        return new DatedAssignmentValidationResult(trimmedIamId, startDate, endDate, createdByAppUserId, null);
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

    private static AdminRoleAssignmentResponse CreateAssignmentResponse(
        bool active,
        string? effectiveEndDate,
        string? effectiveStartDate,
        string id,
        string iamId,
        string? targetId,
        string? targetName,
        string type,
        IReadOnlyDictionary<string, AppUser> usersByIamId)
    {
        var trimmedIamId = iamId.Trim();
        usersByIamId.TryGetValue(trimmedIamId, out var user);

        return new AdminRoleAssignmentResponse(
            Active: active,
            EffectiveEndDate: effectiveEndDate,
            EffectiveStartDate: effectiveStartDate,
            Email: user?.Email ?? string.Empty,
            Id: id,
            IamId: trimmedIamId,
            Name: user?.DisplayName ?? trimmedIamId,
            TargetId: targetId,
            TargetName: targetName,
            Type: type);
    }

    private static bool IsActive(DateOnly startDate, DateOnly? endDate, DateOnly today, DateTime? closedUtc)
    {
        return closedUtc == null && startDate <= today && (!endDate.HasValue || endDate.Value > today);
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private sealed record AdminRolesResponse(
        IReadOnlyList<AdminRoleAssignmentResponse> Assignments,
        IReadOnlyList<AdminRoleOption> Clusters,
        IReadOnlyList<AdminRoleOption> Departments,
        IReadOnlyList<AdminRoleUserOption> Users);
    private sealed record AdminRoleAssignmentResponse(
        bool Active,
        string? EffectiveEndDate,
        string? EffectiveStartDate,
        string Email,
        string Id,
        string IamId,
        string Name,
        string? TargetId,
        string? TargetName,
        string Type);
    private sealed record AdminRoleOption(string Id, string Name);
    private sealed record AdminRoleUserOption(string Email, string IamId, string Name);
    public sealed record AddAdminRequest(string? IamId);
    public sealed record AddCaoRequest(int ClusterId, string? EffectiveEndDate, string? EffectiveStartDate, string? IamId);
    public sealed record AddChairRequest(string? DepartmentCode, string? EffectiveEndDate, string? EffectiveStartDate, string? IamId);
    private sealed record DatedAssignmentValidationResult(
        string? IamId,
        DateOnly? StartDate,
        DateOnly? EndDate,
        int? CreatedByAppUserId,
        IActionResult? Error)
    {
        public static DatedAssignmentValidationResult WithError(IActionResult error)
        {
            return new DatedAssignmentValidationResult(null, null, null, null, error);
        }
    }
}
