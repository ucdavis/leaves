using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
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

        var leaveTypes = await _db.LeaveTypes
            .AsNoTracking()
            .ToDictionaryAsync(type => type.Id, cancellationToken);

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

        var activeUsers = users.Where(user => user.IsActive).ToList();
        var activeUsersWithDepartments = activeUsers
            .Where(user => latestDepartmentByUserId.ContainsKey(user.Id))
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

        var requestsByType = leaveRequests
            .GroupBy(request =>
            {
                if (leaveTypes.TryGetValue(request.LeaveTypeId, out var leaveType))
                {
                    return leaveType.DisplayName;
                }

                return "Unknown";
            })
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        var pendingRequests = leaveRequests.Count(request => request.Status == LeaveRequestStatus.PendingApproval);

        var response = new AdminDashboardResponse(
            Clusters: clusterResponses,
            DataSources: new[]
            {
                new AdminDataSourceResponse("db-users", "App users", "Sourced from AppUser records.", "ready", GetLatestTimestamp(users.Select(user => user.UpdatedUtc))),
                new AdminDataSourceResponse("db-departments", "Departments", "Sourced from Department and DepartmentEmailRouting.", "ready", GetLatestTimestamp(departments.Select(department => department.UpdatedUtc))),
                new AdminDataSourceResponse("db-requests", "Leave requests", "Sourced from LeaveRequest history snapshots.", leaveRequests.Count > 0 ? "ready" : "planned", GetLatestTimestamp(leaveRequests.Select(request => request.UpdatedUtc))),
            },
            Departments: departmentResponses,
            ReadonlyReason: "Chair, CAO, designation, and disposition fields are not modeled in the current database yet, so this admin UI now reads from the database and only enables persisted fields.",
            StatusSnapshot: new AdminStatusSnapshotResponse(
                Departments: new DepartmentStatusResponse(
                    Clustered: departments.Count(department => department.ClusterId.HasValue),
                    Total: departments.Count,
                    WithFaculty: activeUsersWithDepartments
                        .Select(user => latestDepartmentByUserId[user.Id])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()),
                Issues: new AdminIssuesResponse(
                    ApproachingVacationCap: 0,
                    ExcludedUsers: users.Count(user => !user.IsActive),
                    FacultyAtVacationCap: 0,
                    MissingEmails: activeUsers.Count(user => string.IsNullOrWhiteSpace(user.Email)),
                    PendingRequests: pendingRequests),
                Requests: new AdminRequestStatusResponse(
                    BySource: new RequestSourceStatusResponse(
                        Cognos: 0,
                        Manual: leaveRequests.Count),
                    ByType: requestsByType,
                    Pending: pendingRequests,
                    Total: leaveRequests.Count),
                Users: new AdminUserStatusResponse(
                    Admins: activeUsers.Count(user => adminIamIds.Contains(user.IamId.Trim())),
                    AyFaculty: 0,
                    Caos: 0,
                    Chairs: 0,
                    FyFaculty: activeUsers.Count(user => !adminIamIds.Contains(user.IamId.Trim())),
                    Total: activeUsers.Count)),
            Users: userResponses);

        return Ok(response);
    }

    [HttpPatch("clusters/{id:int}")]
    public async Task<IActionResult> UpdateCluster(int id, [FromBody] UpdateClusterRequest request, CancellationToken cancellationToken)
    {
        var cluster = await _db.Clusters.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (cluster == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            cluster.ClusterName = request.Name.Trim();
        }

        cluster.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("departments/{departmentCode}")]
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

    [HttpPost("departments/{departmentCode}/routing-emails")]
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
            var adminUserId = await GetFallbackAdminUserId(cancellationToken);
            if (adminUserId == null)
            {
                return ValidationProblem("An AppUser row is required before routing emails can be updated.");
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

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("departments/{departmentCode}/routing-emails/{id:int}")]
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

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var iamId = request.IamId?.Trim();
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

        if (request.EmployeeIdSet)
        {
            user.EmployeeId = NullIfWhiteSpace(request.EmployeeId);
        }

        if (request.IamIdSet)
        {
            var iamId = request.IamId?.Trim();
            if (string.IsNullOrWhiteSpace(iamId))
            {
                return ValidationProblem("IAM ID cannot be empty.");
            }

            user.IamId = iamId;
        }

        user.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new AdminStatusResponse("Admin access granted."));
    }

    private async Task<int?> GetFallbackAdminUserId(CancellationToken cancellationToken)
    {
        var adminIamId = await _db.AppAdminAssignments
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.IamId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(adminIamId))
        {
            return await _db.AppUsers
                .AsNoTracking()
                .Where(user => user.IamId == adminIamId)
                .Select(user => (int?)user.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? GetLatestTimestamp(IEnumerable<DateTime> timestamps)
    {
        var latest = timestamps.DefaultIfEmpty().Max();
        if (latest == default)
        {
            return null;
        }

        return latest.ToString("O");
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
    private sealed record AdminDashboardResponse(
        IReadOnlyList<AdminClusterResponse> Clusters,
        IReadOnlyList<AdminDataSourceResponse> DataSources,
        IReadOnlyList<AdminDepartmentResponse> Departments,
        string ReadonlyReason,
        AdminStatusSnapshotResponse StatusSnapshot,
        IReadOnlyList<AdminUserResponse> Users);
    private sealed record AdminClusterResponse(string? CaoUserId, string Id, string Name);
    private sealed record AdminDataSourceResponse(string Id, string Label, string Detail, string Status, string? UpdatedAt);
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
    private sealed record AdminStatusSnapshotResponse(
        DepartmentStatusResponse Departments,
        AdminIssuesResponse Issues,
        AdminRequestStatusResponse Requests,
        AdminUserStatusResponse Users);
    private sealed record DepartmentStatusResponse(int Clustered, int Total, int WithFaculty);
    private sealed record AdminIssuesResponse(
        int ApproachingVacationCap,
        int ExcludedUsers,
        int FacultyAtVacationCap,
        int MissingEmails,
        int PendingRequests);
    private sealed record AdminRequestStatusResponse(
        RequestSourceStatusResponse BySource,
        IReadOnlyDictionary<string, int> ByType,
        int Pending,
        int Total);
    private sealed record RequestSourceStatusResponse(int Cognos, int Manual);
    private sealed record AdminUserStatusResponse(int Admins, int AyFaculty, int Caos, int Chairs, int FyFaculty, int Total);
    public sealed record UpdateClusterRequest(string? Name);
    public sealed record UpdateDepartmentRequest(string? Name, int? ClusterId, bool ClusterIdSet, string? ApprovalMode);
    public sealed record UpsertRoutingEmailRequest(string? Address);
    public sealed record CreateUserRequest(bool Active, string? Email, string? EmployeeId, string? IamId, string? Name);
    public sealed record UpdateUserRequest(
        bool? Active,
        string? Email,
        bool EmailSet,
        string? EmployeeId,
        bool EmployeeIdSet,
        string? IamId,
        bool IamIdSet,
        string? Name,
        bool NameSet);
}
