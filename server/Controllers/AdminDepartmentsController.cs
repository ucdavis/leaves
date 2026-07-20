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
    private readonly AppDbContext _db;
    private readonly AdminDataService _adminDataService;

    public AdminDepartmentsController(AppDbContext db, AdminDataService adminDataService)
    {
        _db = db;
        _adminDataService = adminDataService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        return Ok(await _adminDataService.GetDepartmentsAsync(cancellationToken));
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
            cluster => cluster.ClusterName == name,
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
            var clusterExists = await _db.Clusters.AnyAsync(
                cluster => cluster.Id == request.ClusterId.Value,
                cancellationToken);
            if (!clusterExists)
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
            department.ClusterId = request.ClusterId;
        }

        if (!string.IsNullOrWhiteSpace(request.ApprovalMode))
        {
            department.WorkflowMode = request.ApprovalMode == "approval"
                ? WorkflowMode.ApprovalRequired
                : WorkflowMode.DirectSubmission;
        }

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

        if (department == null)
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

        return await _db.AppUsers
            .AsNoTracking()
            .Where(user => user.EntraObjectId == entraObjectId)
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsDuplicateDepartmentRoutingEmail(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    public sealed record CreateClusterRequest(string? Name);
    public sealed record CreateDepartmentRequest(string? ApprovalMode, int? ClusterId, string? Code, string? Name);
    public sealed record UpdateDepartmentRequest(string? Name, int? ClusterId, bool ClusterIdSet, string? ApprovalMode);
    public sealed record UpsertRoutingEmailRequest(string? Address);
}
