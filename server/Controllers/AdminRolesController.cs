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
    private readonly AdminDirectoryService _directoryService;

    public AdminRolesController(AppDbContext db, AdminDirectoryService directoryService)
    {
        _db = db;
        _directoryService = directoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRolesAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentAccrualRows = await _directoryService.LoadCurrentAccrualRowsAsync(cancellationToken);
        var currentAccrualGroups = currentAccrualRows
            .Where(row => !string.IsNullOrWhiteSpace(row.EmployeeId))
            .GroupBy(row => NormalizeEmployeeId(row.EmployeeId)!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var latestAccrualByEmployeeId = currentAccrualGroups
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.AsOfDate)
                    .ThenByDescending(row => row.LastUpdated)
                    .ThenBy(row => row.LeaveTypeNumber)
                    .ThenBy(row => row.PositionNumber, StringComparer.OrdinalIgnoreCase)
                    .First(),
                StringComparer.OrdinalIgnoreCase);
        var currentDepartmentCodesByEmployeeId = currentAccrualGroups
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var latestAsOfDate = group.Max(row => row.AsOfDate);
                    return (IReadOnlyList<string>)group
                        .Where(row => row.AsOfDate == latestAsOfDate)
                        .Select(row => row.Level5Dept.Trim())
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                },
                StringComparer.OrdinalIgnoreCase);

        var people = await _db.People
            .AsNoTracking()
            .Where(person =>
                person.EmployeeId != null &&
                _db.EmployeeAccrualBalances.Any(row => row.EmployeeId == person.EmployeeId))
            .OrderBy(person => person.FullName)
            .ThenBy(person => person.IamId)
            .Select(person => new Person
            {
                IamId = person.IamId,
                EmployeeId = person.EmployeeId,
                Email = person.Email,
                FullName = person.FullName,
            })
            .ToListAsync(cancellationToken);
        var appUsersByIamId = (await _db.AppUsers
                .AsNoTracking()
                .Where(user => !string.IsNullOrWhiteSpace(user.IamId) &&
                               ((user.EmployeeId != null &&
                                 _db.EmployeeAccrualBalances.Any(row => row.EmployeeId == user.EmployeeId)) ||
                                _db.AppAdminAssignments.Any(assignment => assignment.IamId == user.IamId) ||
                                _db.ClusterCaoAssignments.Any(assignment => assignment.IamId == user.IamId) ||
                                _db.DepartmentChairAssignments.Any(assignment => assignment.IamId == user.IamId)))
                .OrderBy(user => user.DisplayName)
                .ThenBy(user => user.IamId)
                .Select(user => new AppUser
                {
                    IamId = user.IamId,
                    EmployeeId = user.EmployeeId,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(user => user.IamId.Trim(), StringComparer.OrdinalIgnoreCase);
        var peopleByEmployeeId = people
            .Where(person => !string.IsNullOrWhiteSpace(person.EmployeeId))
            .GroupBy(person => NormalizeEmployeeId(person.EmployeeId)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var appUsersByEmployeeId = appUsersByIamId.Values
            .Where(user => user.EmployeeId != null)
            .GroupBy(user => NormalizeEmployeeId(user.EmployeeId)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var departments = await _db.Departments
            .AsNoTracking()
            .OrderBy(department => department.DepartmentName)
            .Select(department => new Department
            {
                DepartmentCode = department.DepartmentCode,
                DepartmentName = department.DepartmentName,
            })
            .ToListAsync(cancellationToken);
        var departmentsByCode = departments.ToDictionary(department => department.DepartmentCode, StringComparer.OrdinalIgnoreCase);
        var currentOverridesByIamId = await GetCurrentDepartmentOverridesByIamId(today, cancellationToken);

        var clusters = await _db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.IsActive)
            .OrderBy(cluster => cluster.ClusterName)
            .Select(cluster => new Cluster
            {
                Id = cluster.Id,
                ClusterName = cluster.ClusterName,
            })
            .ToListAsync(cancellationToken);
        var clustersById = clusters.ToDictionary(cluster => cluster.Id);

        var adminAssignments = _db.AppAdminAssignments
            .AsNoTracking()
            .Select(assignment => new
            {
                Type = "admin",
                assignment.Id,
                assignment.IamId,
                EffectiveStartDate = (DateOnly?)null,
                EffectiveEndDateExclusive = (DateOnly?)null,
                ClosedUtc = (DateTime?)null,
                ClusterId = (int?)null,
                DepartmentCode = (string?)null,
            });
        var caoAssignments = _db.ClusterCaoAssignments
            .AsNoTracking()
            .Select(assignment => new
            {
                Type = "cao",
                assignment.Id,
                assignment.IamId,
                EffectiveStartDate = (DateOnly?)assignment.EffectiveStartDate,
                assignment.EffectiveEndDateExclusive,
                assignment.ClosedUtc,
                ClusterId = (int?)assignment.ClusterId,
                DepartmentCode = (string?)null,
            });
        var chairAssignments = _db.DepartmentChairAssignments
            .AsNoTracking()
            .Select(assignment => new
            {
                Type = "chair",
                assignment.Id,
                assignment.IamId,
                EffectiveStartDate = (DateOnly?)assignment.EffectiveStartDate,
                assignment.EffectiveEndDateExclusive,
                assignment.ClosedUtc,
                ClusterId = (int?)null,
                DepartmentCode = (string?)assignment.DepartmentCode,
            });
        var assignmentRows = await adminAssignments
            .Concat(caoAssignments)
            .Concat(chairAssignments)
            .ToListAsync(cancellationToken);

        var assignments = assignmentRows
            .Select(assignment =>
            {
                if (assignment.Type == "admin")
                {
                    return CreateAssignmentResponse(
                        active: true,
                        effectiveEndDate: null,
                        effectiveStartDate: null,
                        id: assignment.Id.ToString(),
                        iamId: assignment.IamId,
                        targetId: null,
                        targetName: null,
                        type: assignment.Type,
                        appUsersByIamId: appUsersByIamId);
                }

                if (assignment.Type == "cao")
                {
                    var clusterId = assignment.ClusterId!.Value;
                    clustersById.TryGetValue(clusterId, out var cluster);
                    return CreateAssignmentResponse(
                        active: IsActive(
                            assignment.EffectiveStartDate!.Value,
                            assignment.EffectiveEndDateExclusive,
                            today,
                            assignment.ClosedUtc),
                        effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                        effectiveStartDate: assignment.EffectiveStartDate.Value.ToString("yyyy-MM-dd"),
                        id: assignment.Id.ToString(),
                        iamId: assignment.IamId,
                        targetId: clusterId.ToString(),
                        targetName: cluster?.ClusterName ?? $"Cluster {clusterId}",
                        type: assignment.Type,
                        appUsersByIamId: appUsersByIamId);
                }

                var departmentCode = assignment.DepartmentCode!;
                departmentsByCode.TryGetValue(departmentCode, out var department);
                return CreateAssignmentResponse(
                    active: IsActive(
                        assignment.EffectiveStartDate!.Value,
                        assignment.EffectiveEndDateExclusive,
                        today,
                        assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.Value.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: departmentCode,
                    targetName: department?.DepartmentName ?? departmentCode,
                    type: assignment.Type,
                    appUsersByIamId: appUsersByIamId);
            })
            .OrderByDescending(assignment => assignment.Active)
            .ThenBy(assignment => assignment.Type)
            .ThenBy(assignment => assignment.TargetName)
            .ThenBy(assignment => assignment.Name)
            .ThenBy(assignment => assignment.IamId)
            .ThenBy(assignment => assignment.Id)
            .ToList();

        var users = latestAccrualByEmployeeId
            .OrderBy(item => item.Value.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var employeeId = item.Key;
                var latestAccrual = item.Value;
                peopleByEmployeeId.TryGetValue(employeeId, out var person);
                appUsersByEmployeeId.TryGetValue(employeeId, out var appUser);

                var iamId = appUser?.IamId?.Trim()
                    ?? person?.IamId?.Trim()
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(iamId))
                {
                    return null;
                }

                var lookupIamId = NormalizeKey(iamId);
                currentOverridesByIamId.TryGetValue(lookupIamId, out var currentOverride);

                var departmentOptions = BuildDepartmentOptions(
                    currentOverride,
                    employeeId,
                    currentDepartmentCodesByEmployeeId,
                    departmentsByCode);
                var departmentCode = departmentOptions.FirstOrDefault()?.Id;
                var departmentName = !string.IsNullOrWhiteSpace(departmentCode) &&
                                     departmentsByCode.TryGetValue(departmentCode, out var department)
                    ? department.DepartmentName
                    : null;

                return new AdminRoleUserOption(
                    DepartmentId: departmentCode,
                    DepartmentName: departmentName,
                    DepartmentOptions: departmentOptions,
                    Email: appUser?.Email ?? person?.Email ?? latestAccrual.EmployeeEmail ?? string.Empty,
                    IamId: iamId,
                    Name: appUser?.DisplayName ?? person?.FullName ?? latestAccrual.EmployeeName);
            })
            .Where(user => user != null)
            .Select(user => user!)
            .ToList();

        return Ok(new AdminRolesResponse(
            Assignments: assignments,
            Clusters: clusters.Select(cluster => new AdminRoleOption(cluster.Id.ToString(), cluster.ClusterName)).ToList(),
            Departments: departments.Select(department => new AdminRoleOption(department.DepartmentCode, department.DepartmentName)).ToList(),
            Users: users));
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

        var clusterExists = await _db.Clusters.AnyAsync(cluster => cluster.Id == request.ClusterId, cancellationToken);
        if (!clusterExists)
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
        var departmentExists = await _db.Departments.AnyAsync(department => department.DepartmentCode == departmentCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(departmentCode) || !departmentExists)
        {
            return ValidationProblem("Selected department does not exist.");
        }

        var personDepartmentCodes = await GetCurrentDepartmentCodesAsync(validationResult.IamId!, validationResult.StartDate!.Value, cancellationToken);
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

    private async Task<Dictionary<string, EmployeeReportingDepartmentOverride>> GetCurrentDepartmentOverridesByIamId(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var activeOverridesQuery = _db.EmployeeReportingDepartmentOverrides
            .AsNoTracking()
            .Where(item => item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today));
        var activeOverrides = await activeOverridesQuery
            .Where(item => !activeOverridesQuery.Any(other =>
                other.IamId == item.IamId &&
                (other.EffectiveStartDate > item.EffectiveStartDate ||
                 (other.EffectiveStartDate == item.EffectiveStartDate && other.Id > item.Id))))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return activeOverrides
            .GroupBy(item => NormalizeKey(item.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<string>> GetCurrentDepartmentCodesAsync(
        string iamId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var currentOverrideDepartmentCode = await _db.EmployeeReportingDepartmentOverrides
            .AsNoTracking()
            .Where(item => item.IamId == iamId &&
                           item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .Select(item => item.DepartmentCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(currentOverrideDepartmentCode))
        {
            return [currentOverrideDepartmentCode.Trim()];
        }

        var employeeId = NormalizeEmployeeId(await _db.People
            .AsNoTracking()
            .Where(item => item.IamId == iamId)
            .Select(item => item.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken));
        if (employeeId == null)
        {
            return [];
        }

        var latestAsOfDate = await _db.EmployeeAccrualBalances
            .Where(row => row.EmployeeId == employeeId)
            .OrderByDescending(row => row.AsOfDate)
            .Select(row => (DateOnly?)row.AsOfDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (!latestAsOfDate.HasValue)
        {
            return [];
        }

        var departmentCodes = await _db.EmployeeAccrualBalances
            .Where(row => row.EmployeeId == employeeId &&
                          row.AsOfDate == latestAsOfDate.Value &&
                          !string.IsNullOrWhiteSpace(row.Level5Dept))
            .Select(row => row.Level5Dept.Trim())
            .Distinct()
            .ToListAsync(cancellationToken);

        return departmentCodes
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        IReadOnlyDictionary<string, AppUser> appUsersByIamId)
    {
        var trimmedIamId = iamId.Trim();
        appUsersByIamId.TryGetValue(trimmedIamId, out var appUser);

        return new AdminRoleAssignmentResponse(
            Active: active,
            EffectiveEndDate: effectiveEndDate,
            EffectiveStartDate: effectiveStartDate,
            Email: appUser?.Email ?? string.Empty,
            Id: id,
            IamId: trimmedIamId,
            Name: appUser?.DisplayName ?? trimmedIamId,
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

    private static IReadOnlyList<AdminRoleOption> BuildDepartmentOptions(
        EmployeeReportingDepartmentOverride? currentOverride,
        string employeeId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> currentDepartmentCodesByEmployeeId,
        IReadOnlyDictionary<string, Department> departmentsByCode)
    {
        if (!string.IsNullOrWhiteSpace(currentOverride?.DepartmentCode))
        {
            var departmentCode = currentOverride.DepartmentCode.Trim();
            return
            [
                new AdminRoleOption(
                    departmentCode,
                    departmentsByCode.TryGetValue(departmentCode, out var department)
                        ? department.DepartmentName
                        : departmentCode),
            ];
        }

        if (!currentDepartmentCodesByEmployeeId.TryGetValue(employeeId, out var departmentCodes))
        {
            return [];
        }

        return departmentCodes
            .Select(departmentCode => new AdminRoleOption(
                departmentCode,
                departmentsByCode.TryGetValue(departmentCode, out var department)
                    ? department.DepartmentName
                    : departmentCode))
            .ToList();
    }

    private static string NormalizeKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string? NormalizeEmployeeId(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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
    private sealed record AdminRoleUserOption(string? DepartmentId, string? DepartmentName, IReadOnlyList<AdminRoleOption> DepartmentOptions, string Email, string IamId, string Name);
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
