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
[Route("api/admin/departments")]
public sealed class AdminDepartmentsController : ApiControllerBase
{
    private const int ClusterNameMaxLength = 100;
    private const int DepartmentCodeMaxLength = 10;
    private const int DepartmentNameMaxLength = 100;
    private readonly AdminDirectoryDataService _adminDirectoryDataService;
    private readonly AdminDirectoryService _adminDirectoryService;
    private readonly AppDbContext _db;
    private readonly IUserService _userService;

    public AdminDepartmentsController(
        AppDbContext db,
        AdminDirectoryDataService adminDirectoryDataService,
        AdminDirectoryService adminDirectoryService,
        IUserService userService)
    {
        _db = db;
        _adminDirectoryDataService = adminDirectoryDataService;
        _adminDirectoryService = adminDirectoryService;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        return Ok(await _adminDirectoryService.GetDepartmentsAsync(cancellationToken));
    }

    [HttpPost("clusters")]
    public async Task<IActionResult> CreateCluster([FromBody] CreateClusterRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationProblem("Cluster name is required.");
        }

        if (name.Length > ClusterNameMaxLength)
        {
            return ValidationProblem($"Cluster name must be {ClusterNameMaxLength} characters or fewer.");
        }

        var duplicateExists = await _db.Clusters.AnyAsync(
            cluster => cluster.IsActive && cluster.ClusterName == name,
            cancellationToken);
        if (duplicateExists)
        {
            return Conflict("A cluster with that name already exists.");
        }

        var now = DateTime.UtcNow;
        var createdByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);

        _db.Clusters.Add(new Cluster
        {
            ClusterName = name,
            CreatedByAppUserId = createdByAppUserId,
            CreatedUtc = now,
            IsActive = true,
            UpdatedUtc = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("clusters/{clusterId:int}")]
    public async Task<IActionResult> UpdateCluster(int clusterId, [FromBody] UpdateClusterRequest request, CancellationToken cancellationToken)
    {
        var cluster = await _db.Clusters.FirstOrDefaultAsync(
            item => item.Id == clusterId,
            cancellationToken);

        if (cluster == null || !cluster.IsActive)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (name.Length > ClusterNameMaxLength)
            {
                return ValidationProblem($"Cluster name must be {ClusterNameMaxLength} characters or fewer.");
            }

            var duplicateExists = await _db.Clusters.AnyAsync(
                item => item.Id != clusterId && item.IsActive && item.ClusterName == name,
                cancellationToken);
            if (duplicateExists)
            {
                return Conflict("A cluster with that name already exists.");
            }

            cluster.ClusterName = name;
        }

        if (request.CaoUserIdSet)
        {
            var caoUpdateResult = await UpdateClusterCaoAssignmentAsync(
                cluster.Id,
                request.CaoUserId,
                cancellationToken);
            if (caoUpdateResult != null)
            {
                return caoUpdateResult;
            }
        }

        cluster.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("clusters/{clusterId:int}")]
    public async Task<IActionResult> DeleteCluster(int clusterId, CancellationToken cancellationToken)
    {
        var cluster = await _db.Clusters
            .Include(item => item.Departments)
            .FirstOrDefaultAsync(item => item.Id == clusterId, cancellationToken);

        if (cluster == null)
        {
            return NotFound();
        }

        var caoUpdateResult = await UpdateClusterCaoAssignmentAsync(cluster.Id, null, cancellationToken);
        if (caoUpdateResult != null)
        {
            return caoUpdateResult;
        }

        var now = DateTime.UtcNow;
        foreach (var department in cluster.Departments.ToList())
        {
            department.ClusterId = null;
            department.UpdatedUtc = now;
        }

        cluster.IsActive = false;
        cluster.UpdatedUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var departmentCode = request.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return ValidationProblem("Department code is required.");
        }

        if (departmentCode.Length > DepartmentCodeMaxLength)
        {
            return ValidationProblem($"Department code must be {DepartmentCodeMaxLength} characters or fewer.");
        }

        if (!departmentCode.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-'))
        {
            return ValidationProblem("Department code must use only letters, numbers, underscores, or hyphens.");
        }

        var departmentName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            return ValidationProblem("Department name is required.");
        }

        if (departmentName.Length > DepartmentNameMaxLength)
        {
            return ValidationProblem($"Department name must be {DepartmentNameMaxLength} characters or fewer.");
        }

        if (request.ClusterId.HasValue)
        {
            var cluster = await _db.Clusters.FirstOrDefaultAsync(
                item => item.Id == request.ClusterId.Value,
                cancellationToken);
            if (cluster == null || !cluster.IsActive)
            {
                return ValidationProblem("Selected cluster does not exist.");
            }
        }

        var departmentExists = await _db.Departments.AnyAsync(
            department => department.DepartmentCode == departmentCode,
            cancellationToken);
        if (departmentExists)
        {
            return Conflict("A department with that code already exists.");
        }

        var now = DateTime.UtcNow;
        _db.Departments.Add(new Department
        {
            ClusterId = request.ClusterId,
            CreatedUtc = now,
            DepartmentCode = departmentCode,
            DepartmentName = departmentName,
            IsActive = true,
            UpdatedUtc = now,
            WorkflowMode = request.ApprovalMode == "approval"
                ? WorkflowMode.ApprovalRequired
                : WorkflowMode.DirectSubmission,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("{departmentCode}")]
    public async Task<IActionResult> UpdateDepartment(string departmentCode, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode,
            cancellationToken);

        if (department == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            department.DepartmentName = request.Name.Trim();
        }

        if (request.ClusterIdSet)
        {
            if (request.ClusterId.HasValue)
            {
                var cluster = await _db.Clusters.FirstOrDefaultAsync(
                    item => item.Id == request.ClusterId.Value,
                    cancellationToken);
                if (cluster == null || !cluster.IsActive)
                {
                    return ValidationProblem("Selected cluster does not exist.");
                }
            }

            department.ClusterId = request.ClusterId;
        }

        if (!string.IsNullOrWhiteSpace(request.ApprovalMode))
        {
            department.WorkflowMode = request.ApprovalMode == "approval"
                ? WorkflowMode.ApprovalRequired
                : WorkflowMode.DirectSubmission;
        }

        if (request.ChairUserIdSet)
        {
            var chairUpdateResult = await UpdateDepartmentChairAssignmentAsync(
                department.DepartmentCode,
                request.ChairUserId,
                cancellationToken);
            if (chairUpdateResult != null)
            {
                return chairUpdateResult;
            }
        }

        department.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{departmentCode}")]
    public async Task<IActionResult> DeleteDepartment(string departmentCode, CancellationToken cancellationToken)
    {
        var department = await _db.Departments
            .FirstOrDefaultAsync(item => item.DepartmentCode == departmentCode, cancellationToken);

        if (department == null || !department.IsActive)
        {
            return NotFound();
        }

        var chairUpdateResult = await UpdateDepartmentChairAssignmentAsync(
            department.DepartmentCode,
            null,
            cancellationToken);
        if (chairUpdateResult != null)
        {
            return chairUpdateResult;
        }

        department.IsActive = false;
        department.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{departmentCode}/routing-emails")]
    public async Task<IActionResult> AddRoutingEmail(string departmentCode, [FromBody] UpsertRoutingEmailRequest request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode,
            cancellationToken);

        if (department == null || !department.IsActive)
        {
            return NotFound();
        }

        var email = request.Address?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ValidationProblem("Email address is required.");
        }

        var existing = await _db.DepartmentEmailRoutings.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode && item.ToEmail == email,
            cancellationToken);

        if (existing == null)
        {
            var adminUserId = await GetAuthenticatedAppUserId(cancellationToken);
            if (adminUserId == null)
            {
                return ValidationProblem("The authenticated admin must have an AppUser row before routing emails can be updated.");
            }

            existing = new DepartmentEmailRouting
            {
                DepartmentCode = departmentCode,
                IsActive = true,
                ToEmail = email,
                UpdatedByAppUserId = adminUserId.Value,
                UpdatedUtc = DateTime.UtcNow,
            };

            _db.DepartmentEmailRoutings.Add(existing);
        }
        else
        {
            existing.IsActive = true;
            existing.UpdatedUtc = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateDepartmentRoutingEmail(ex))
        {
            return Conflict("That routing email already exists for this department.");
        }

        return NoContent();
    }

    [HttpDelete("{departmentCode}/routing-emails/{id:int}")]
    public async Task<IActionResult> RemoveRoutingEmail(string departmentCode, int id, CancellationToken cancellationToken)
    {
        var routing = await _db.DepartmentEmailRoutings.FirstOrDefaultAsync(
            item => item.Id == id && item.DepartmentCode == departmentCode,
            cancellationToken);

        if (routing == null)
        {
            return NotFound();
        }

        _db.DepartmentEmailRoutings.Remove(routing);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
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

    private async Task<IActionResult?> UpdateClusterCaoAssignmentAsync(
        int clusterId,
        string? caoUserId,
        CancellationToken cancellationToken)
    {
        var adminUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (adminUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before cluster CAOs can be updated.");
        }

        var normalizedCaoUserId = caoUserId?.Trim();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentAssignment = await _db.ClusterCaoAssignments
            .Where(item => item.ClusterId == clusterId &&
                           item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(normalizedCaoUserId))
        {
            if (currentAssignment != null)
            {
                AdminRolesService.CloseClusterCaoAssignment(currentAssignment, adminUserId.Value, now, today);
            }

            return null;
        }

        var userExists = await _adminDirectoryDataService.DirectoryUserExistsAsync(
            normalizedCaoUserId,
            cancellationToken);
        if (!userExists)
        {
            return ValidationProblem("Selected CAO must be a valid directory user.");
        }

        if (currentAssignment != null &&
            string.Equals(currentAssignment.IamId, normalizedCaoUserId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hasDuplicateActiveAssignment = await HasActiveClusterCaoAssignmentAsync(
            clusterId,
            normalizedCaoUserId,
            today,
            cancellationToken,
            currentAssignment?.Id);
        if (hasDuplicateActiveAssignment)
        {
            return Conflict("That user already has an active CAO assignment for this cluster.");
        }

        if (currentAssignment != null)
        {
            AdminRolesService.CloseClusterCaoAssignment(currentAssignment, adminUserId.Value, now, today);
        }

        _db.ClusterCaoAssignments.Add(new ClusterCaoAssignment
        {
            ClusterId = clusterId,
            CreatedByAppUserId = adminUserId.Value,
            CreatedUtc = now,
            EffectiveStartDate = today,
            IamId = normalizedCaoUserId,
        });

        return null;
    }

    private async Task<IActionResult?> UpdateDepartmentChairAssignmentAsync(
        string departmentCode,
        string? chairUserId,
        CancellationToken cancellationToken)
    {
        var adminUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (adminUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before department chairs can be updated.");
        }

        var normalizedChairUserId = chairUserId?.Trim();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentAssignment = await _db.DepartmentChairAssignments
            .Where(item => item.DepartmentCode == departmentCode &&
                           item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(normalizedChairUserId))
        {
            if (currentAssignment != null)
            {
                AdminRolesService.CloseDepartmentChairAssignment(currentAssignment, adminUserId.Value, now, today);
            }

            return null;
        }

        var userBelongsToDepartment = await _adminDirectoryDataService.UserBelongsToDepartmentAsync(
            normalizedChairUserId,
            departmentCode,
            cancellationToken);
        if (!userBelongsToDepartment)
        {
            return ValidationProblem("Selected chair must currently belong to this department.");
        }

        if (currentAssignment != null &&
            string.Equals(currentAssignment.IamId, normalizedChairUserId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hasDuplicateActiveAssignment = await HasActiveDepartmentChairAssignmentAsync(
            departmentCode,
            normalizedChairUserId,
            today,
            cancellationToken,
            currentAssignment?.Id);
        if (hasDuplicateActiveAssignment)
        {
            return Conflict("That user already has an active department chair assignment for this department.");
        }

        if (currentAssignment != null)
        {
            AdminRolesService.CloseDepartmentChairAssignment(currentAssignment, adminUserId.Value, now, today);
        }

        _db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            CreatedByAppUserId = adminUserId.Value,
            CreatedUtc = now,
            DepartmentCode = departmentCode,
            EffectiveStartDate = today,
            IamId = normalizedChairUserId,
        });

        return null;
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

    private static bool IsDuplicateDepartmentRoutingEmail(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    public sealed record CreateClusterRequest(string? Name);
    public sealed record UpdateClusterRequest(string? Name, string? CaoUserId, bool CaoUserIdSet);
    public sealed record CreateDepartmentRequest(string? ApprovalMode, int? ClusterId, string? Code, string? Name);
    public sealed record UpdateDepartmentRequest(string? Name, int? ClusterId, bool ClusterIdSet, string? ApprovalMode, string? ChairUserId, bool ChairUserIdSet);
    public sealed record UpsertRoutingEmailRequest(string? Address);
}
