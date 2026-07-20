using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

namespace Server.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/admin/departments")]
public sealed class AdminDepartmentsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public AdminDepartmentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var clusters = await _db.Clusters
            .AsNoTracking()
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);

        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings)
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);

        var users = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);

        var adminIamIds = await _db.AppAdminAssignments
            .AsNoTracking()
            .Select(assignment => assignment.IamId.Trim())
            .ToHashSetAsync(cancellationToken);

        var leaveRequests = await _db.LeaveRequests
            .AsNoTracking()
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .ToListAsync(cancellationToken);

        var latestDepartmentByUserId = leaveRequests
            .GroupBy(request => request.AppUserId)
            .ToDictionary(
                group => group.Key,
                group => group.First().ReportingDepartmentCodeSnapshot);

        var departmentResponses = departments
            .Select(department => new AdminDepartmentResponse(
                ApprovalMode: department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "notification",
                ChairUserId: null,
                ClusterId: department.ClusterId?.ToString(),
                Code: department.DepartmentCode,
                DispositionRequired: false,
                Id: department.DepartmentCode,
                Name: department.DepartmentName,
                RoutingEmails: department.DepartmentEmailRoutings
                    .Where(routing => routing.IsActive)
                    .OrderBy(routing => routing.ToEmail)
                    .Select(routing => new DepartmentRoutingEmailResponse(
                        Address: routing.ToEmail,
                        Id: routing.Id.ToString(),
                        Kind: "to"))
                    .ToList()))
            .ToList();

        var clusterResponses = clusters
            .Select(cluster => new AdminClusterResponse(
                CaoUserId: null,
                Id: cluster.Id.ToString(),
                Name: cluster.ClusterName))
            .ToList();

        var userResponses = users
            .Select(user =>
            {
                var trimmedIamId = user.IamId.Trim();
                var isAdmin = adminIamIds.Contains(trimmedIamId);
                var departmentCode = latestDepartmentByUserId.GetValueOrDefault(user.Id);

                return new AdminUserResponse(
                    Id: user.Id.ToString(),
                    Active: user.IsActive,
                    DepartmentId: departmentCode,
                    Designation: isAdmin ? "admin" : "fy",
                    Email: user.Email ?? string.Empty,
                    EmployeeId: user.EmployeeId?.Trim() ?? string.Empty,
                    IamId: trimmedIamId,
                    Name: user.DisplayName ?? trimmedIamId,
                    Position: string.Empty,
                    Role: isAdmin ? "admin" : "faculty");
            })
            .ToList();

        return Ok(new AdminDepartmentsResponse(
            Clusters: clusterResponses,
            Departments: departmentResponses,
            ReadonlyReason: "Chair, CAO, designation, and disposition fields are not modeled in the current database yet, so this admin UI only enables the fields that persist today.",
            Users: userResponses));
    }

    [HttpPost("clusters")]
    public async Task<IActionResult> CreateCluster([FromBody] CreateClusterRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationProblem("Cluster name is required.");
        }

        if (name.Length > 100)
        {
            return ValidationProblem("Cluster name must be 100 characters or fewer.");
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

        if (departmentCode.Length > 10)
        {
            return ValidationProblem("Department code must be 10 characters or fewer.");
        }

        var departmentName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            return ValidationProblem("Department name is required.");
        }

        if (departmentName.Length > 100)
        {
            return ValidationProblem("Department name must be 100 characters or fewer.");
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

    private sealed record AdminDepartmentsResponse(
        IReadOnlyList<AdminClusterResponse> Clusters,
        IReadOnlyList<AdminDepartmentResponse> Departments,
        string ReadonlyReason,
        IReadOnlyList<AdminUserResponse> Users);
    private sealed record AdminClusterResponse(string? CaoUserId, string Id, string Name);
    private sealed record AdminDepartmentResponse(
        string ApprovalMode,
        string? ChairUserId,
        string? ClusterId,
        string Code,
        bool DispositionRequired,
        string Id,
        string Name,
        IReadOnlyList<DepartmentRoutingEmailResponse> RoutingEmails);
    private sealed record DepartmentRoutingEmailResponse(string Address, string Id, string Kind);
    private sealed record AdminUserResponse(
        string Id,
        bool Active,
        string? DepartmentId,
        string Designation,
        string Email,
        string EmployeeId,
        string IamId,
        string Name,
        string Position,
        string Role);
    public sealed record CreateClusterRequest(string? Name);
    public sealed record CreateDepartmentRequest(string? ApprovalMode, int? ClusterId, string? Code, string? Name);
    public sealed record UpdateDepartmentRequest(string? Name, int? ClusterId, bool ClusterIdSet, string? ApprovalMode);
    public sealed record UpsertRoutingEmailRequest(string? Address);
}
