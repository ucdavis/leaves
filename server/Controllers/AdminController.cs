using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

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
        var currentOverridesByIamId = await GetCurrentDepartmentOverridesByIamId(cancellationToken);

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
                currentOverridesByIamId.TryGetValue(trimmedIamId, out var currentOverride);

                return new AdminUserResponse(
                    Id: user.Id.ToString(),
                    Active: user.IsActive,
                    DepartmentId: departmentCode,
                    DepartmentOverrideEndDate: currentOverride?.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    DepartmentOverrideId: currentOverride?.DepartmentCode,
                    DepartmentOverrideStartDate: currentOverride?.EffectiveStartDate.ToString("yyyy-MM-dd"),
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
            ReadonlyReason: "Chair, CAO, designation, and disposition fields are not modeled in the current database yet, so this admin UI only enables the fields that persist today.",
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
                ? await CloseCurrentDepartmentOverrideAsync(user, cancellationToken)
                : await CreateDepartmentOverrideAsync(user, request, cancellationToken);
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

    private async Task<Dictionary<string, EmployeeReportingDepartmentOverride>> GetCurrentDepartmentOverridesByIamId(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overrides = await _db.EmployeeReportingDepartmentOverrides
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return overrides
            .GroupBy(item => item.IamId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IActionResult?> CreateDepartmentOverrideAsync(
        AppUser user,
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
            IamId = user.IamId.Trim(),
            Reason = "Admin user edit",
        });

        return null;
    }

    private async Task<IActionResult?> CloseCurrentDepartmentOverrideAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentOverride = await _db.EmployeeReportingDepartmentOverrides
            .Where(item => item.IamId == user.IamId.Trim() &&
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
        string? DepartmentOverrideEndDate,
        string? DepartmentOverrideId,
        string? DepartmentOverrideStartDate,
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
