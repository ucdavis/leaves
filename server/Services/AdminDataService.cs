using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminDataService
{
    private readonly AppDbContext _db;

    public AdminDataService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminDepartmentsResponse> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(includeDashboardData: false, cancellationToken);
        return new AdminDepartmentsResponse(
            Clusters: snapshot.Clusters,
            Departments: snapshot.Departments,
            Users: snapshot.Users);
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(includeDashboardData: true, cancellationToken);
        return new AdminDashboardResponse(
            Clusters: snapshot.Clusters,
            DataSources: snapshot.DataSources,
            Departments: snapshot.Departments,
            StatusSnapshot: snapshot.StatusSnapshot!,
            Users: snapshot.Users);
    }

    private async Task<AdminSnapshot> LoadSnapshotAsync(bool includeDashboardData, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var clusters = await _db.Clusters
            .AsNoTracking()
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);

        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings)
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);

        var people = await _db.People
            .OrderBy(person => person.FullName)
            .ThenBy(person => person.IamId)
            .ToListAsync(cancellationToken);

        var adminIamIds = await _db.AppAdminAssignments
            .AsNoTracking()
            .Select(assignment => assignment.IamId.Trim())
            .ToListAsync(cancellationToken);
        var adminIamIdSet = adminIamIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentOverridesByIamId = await GetCurrentDepartmentOverridesByIamId(today, cancellationToken);
        var currentChairAssignmentsByDepartment = await GetCurrentChairAssignmentsByDepartment(today, cancellationToken);
        var currentCaoAssignmentsByCluster = await GetCurrentCaoAssignmentsByCluster(today, cancellationToken);
        var latestAccrualByEmployeeId = await GetLatestAccrualByEmployeeId(cancellationToken);
        var userIdByIamId = people
            .GroupBy(person => NormalizeKey(person.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().IamId.Trim(), StringComparer.OrdinalIgnoreCase);

        var departmentResponses = departments
            .Select(department =>
            {
                currentChairAssignmentsByDepartment.TryGetValue(department.DepartmentCode.Trim(), out var chairAssignment);
                var chairUserId = chairAssignment == null
                    ? null
                    : userIdByIamId.GetValueOrDefault(NormalizeKey(chairAssignment.IamId));

                return new AdminDepartmentResponse(
                    ApprovalMode: department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "notification",
                    ChairUserId: chairUserId,
                    ClusterId: department.ClusterId?.ToString(),
                    Code: department.DepartmentCode,
                    Id: department.DepartmentCode,
                    Name: department.DepartmentName,
                    RoutingEmails: department.DepartmentEmailRoutings
                        .Where(routing => routing.IsActive)
                        .OrderBy(routing => routing.ToEmail)
                        .Select(routing => new DepartmentRoutingEmailResponse(
                            Address: routing.ToEmail,
                            Id: routing.Id.ToString(),
                            Kind: "to"))
                        .ToList());
            })
            .ToList();

        var clusterResponses = clusters
            .Select(cluster =>
            {
                currentCaoAssignmentsByCluster.TryGetValue(cluster.Id, out var caoAssignment);
                var caoUserId = caoAssignment == null
                    ? null
                    : userIdByIamId.GetValueOrDefault(NormalizeKey(caoAssignment.IamId));

                return new AdminClusterResponse(
                    CaoUserId: caoUserId,
                    Id: cluster.Id.ToString(),
                    Name: cluster.ClusterName);
            })
            .ToList();

        var chairIamIds = currentChairAssignmentsByDepartment.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var caoIamIds = currentCaoAssignmentsByCluster.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userResponses = people
            .Select(person =>
            {
                var iamId = person.IamId.Trim();
                var lookupIamId = NormalizeKey(person.IamId);
                var employeeId = NormalizeEmployeeId(person.EmployeeId);
                latestAccrualByEmployeeId.TryGetValue(employeeId ?? string.Empty, out var latestAccrual);
                currentOverridesByIamId.TryGetValue(lookupIamId, out var currentOverride);

                var departmentCode = currentOverride?.DepartmentCode.Trim()
                    ?? latestAccrual?.Level5Dept.Trim();
                var isAdmin = adminIamIdSet.Contains(lookupIamId);
                var isChair = chairIamIds.Contains(lookupIamId);
                var isCao = caoIamIds.Contains(lookupIamId);
                var role = GetRole(isAdmin, isChair, isCao);

                return new AdminUserResponse(
                    Id: iamId,
                    Active: true,
                    DepartmentId: departmentCode,
                    DepartmentOverrideEndDate: currentOverride?.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    DepartmentOverrideId: currentOverride?.DepartmentCode,
                    DepartmentOverrideStartDate: currentOverride?.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    Designation: GetDesignation(role, person, latestAccrual),
                    Email: person.Email ?? string.Empty,
                    EmployeeId: employeeId ?? string.Empty,
                    IamId: iamId,
                    Name: person.FullName ?? iamId,
                    Position: latestAccrual?.JobCodeDescription ?? string.Empty,
                    Role: role);
            })
            .ToList();

        var dataSources = Array.Empty<AdminDataSourceResponse>();
        AdminStatusSnapshotResponse? statusSnapshot = null;

        if (includeDashboardData)
        {
            var leaveTypes = await _db.LeaveTypes
                .AsNoTracking()
                .ToDictionaryAsync(type => type.Id, cancellationToken);

            var leaveRequests = await _db.LeaveRequests
                .AsNoTracking()
                .OrderByDescending(request => request.SubmittedAt)
                .ThenByDescending(request => request.Id)
                .ToListAsync(cancellationToken);

            var requestsByType = leaveRequests
                .GroupBy(request => leaveTypes.TryGetValue(request.LeaveTypeId, out var leaveType) ? leaveType.DisplayName : "Unknown")
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count());

            var pendingRequests = leaveRequests.Count(request => request.Status == LeaveRequestStatus.PendingApproval);
            var activeUsers = userResponses;
            var vacationRows = latestAccrualByEmployeeId.Values
                .Where(row => row.TypeLabel.Contains("Vacation", StringComparison.OrdinalIgnoreCase))
                .ToList();

            dataSources =
            [
                new("db-people", "People", "Sourced from People records, with application roles and overrides joined by IAM ID.", "ready", GetLatestTimestamp(people.Select(person => person.LastFetchedAt ?? person.ModifyDate ?? person.PromotedAt ?? person.FirstIngestedAt).OfType<DateTime>())),
                new("db-departments", "Departments", "Sourced from Department, Cluster, chair, CAO, and routing tables.", "ready", GetLatestTimestamp(departments.Select(department => department.UpdatedUtc))),
                new("db-accruals", "Employee accruals", "Sourced from the latest EmployeeAccrualBalances rows for reporting departments and positions.", latestAccrualByEmployeeId.Count > 0 ? "ready" : "planned", GetLatestTimestamp(latestAccrualByEmployeeId.Values.Select(row => row.LastUpdated))),
                new("db-requests", "Leave requests", "Sourced from LeaveRequest history snapshots.", leaveRequests.Count > 0 ? "ready" : "planned", GetLatestTimestamp(leaveRequests.Select(request => request.UpdatedUtc))),
            ];

            statusSnapshot = new AdminStatusSnapshotResponse(
                Departments: new DepartmentStatusResponse(
                    Clustered: departments.Count(department => department.ClusterId.HasValue),
                    Total: departments.Count,
                    WithFaculty: activeUsers
                        .Where(user => user.Role is "faculty" or "chair")
                        .Select(user => user.DepartmentId)
                        .Where(departmentId => !string.IsNullOrWhiteSpace(departmentId))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()),
                Issues: new AdminIssuesResponse(
                    ApproachingVacationCap: vacationRows.Count(row => IsAffirmative(row.ApproachingMax)),
                    ExcludedUsers: 0,
                    FacultyAtVacationCap: vacationRows.Count(row => row.HoursOverUnderPolicyMax >= 0),
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
                    Admins: activeUsers.Count(user => user.Role == "admin"),
                    AyFaculty: activeUsers.Count(user => user.Designation == "ay"),
                    Caos: activeUsers.Count(user => user.Role == "cao"),
                    Chairs: activeUsers.Count(user => user.Role == "chair"),
                    FyFaculty: activeUsers.Count(user => user.Designation == "fy"),
                    Total: activeUsers.Count));
        }

        return new AdminSnapshot(
            Clusters: clusterResponses,
            DataSources: dataSources,
            Departments: departmentResponses,
            StatusSnapshot: statusSnapshot,
            Users: userResponses);
    }

    private async Task<Dictionary<string, EmployeeReportingDepartmentOverride>> GetCurrentDepartmentOverridesByIamId(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var overrides = await _db.EmployeeReportingDepartmentOverrides
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return overrides
            .GroupBy(item => NormalizeKey(item.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, DepartmentChairAssignment>> GetCurrentChairAssignmentsByDepartment(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.DepartmentChairAssignments
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.DepartmentCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<int, ClusterCaoAssignment>> GetCurrentCaoAssignmentsByCluster(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.ClusterCaoAssignments
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.ClusterId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<Dictionary<string, EmployeeAccrualBalance>> GetLatestAccrualByEmployeeId(CancellationToken cancellationToken)
    {
        var accrualRows = await _db.EmployeeAccrualBalances
            .OrderByDescending(row => row.AsOfDate)
            .ThenByDescending(row => row.LastUpdated)
            .ThenBy(row => row.LeaveTypeNumber)
            .ToListAsync(cancellationToken);

        return accrualRows
            .Where(row => !string.IsNullOrWhiteSpace(row.EmployeeId))
            .GroupBy(row => NormalizeEmployeeId(row.EmployeeId)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetRole(bool isAdmin, bool isChair, bool isCao)
    {
        if (isAdmin)
        {
            return "admin";
        }

        if (isCao)
        {
            return "cao";
        }

        return isChair ? "chair" : "faculty";
    }

    private static string GetDesignation(string role, Person? person, EmployeeAccrualBalance? latestAccrual)
    {
        if (role is "admin" or "cao" or "chair")
        {
            return role;
        }

        if (person?.IsFaculty == false)
        {
            return "nfa";
        }

        var description = latestAccrual?.EmployeeClassDescription ?? string.Empty;
        if (description.Contains("Academic Year", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("AY", StringComparison.OrdinalIgnoreCase))
        {
            return "ay";
        }

        return "fy";
    }

    private static bool IsAffirmative(string? value)
    {
        return string.Equals(value?.Trim(), "Y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "True", StringComparison.OrdinalIgnoreCase);
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

    private static string? GetLatestTimestamp(IEnumerable<DateTime> timestamps)
    {
        var latest = timestamps.DefaultIfEmpty().Max();
        if (latest == default)
        {
            return null;
        }

        return latest.ToString("O");
    }

    private sealed record AdminSnapshot(
        IReadOnlyList<AdminClusterResponse> Clusters,
        IReadOnlyList<AdminDataSourceResponse> DataSources,
        IReadOnlyList<AdminDepartmentResponse> Departments,
        AdminStatusSnapshotResponse? StatusSnapshot,
        IReadOnlyList<AdminUserResponse> Users);
}

public sealed record AdminDashboardResponse(
    IReadOnlyList<AdminClusterResponse> Clusters,
    IReadOnlyList<AdminDataSourceResponse> DataSources,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    AdminStatusSnapshotResponse StatusSnapshot,
    IReadOnlyList<AdminUserResponse> Users);

public sealed record AdminDepartmentsResponse(
    IReadOnlyList<AdminClusterResponse> Clusters,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> Users);

public sealed record AdminClusterResponse(string? CaoUserId, string Id, string Name);
public sealed record AdminDataSourceResponse(string Id, string Label, string Detail, string Status, string? UpdatedAt);

public sealed record AdminDepartmentResponse(
    string ApprovalMode,
    string? ChairUserId,
    string? ClusterId,
    string Code,
    string Id,
    string Name,
    IReadOnlyList<DepartmentRoutingEmailResponse> RoutingEmails);

public sealed record DepartmentRoutingEmailResponse(string Address, string Id, string Kind);

public sealed record AdminUserResponse(
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

public sealed record AdminStatusSnapshotResponse(
    DepartmentStatusResponse Departments,
    AdminIssuesResponse Issues,
    AdminRequestStatusResponse Requests,
    AdminUserStatusResponse Users);

public sealed record DepartmentStatusResponse(int Clustered, int Total, int WithFaculty);

public sealed record AdminIssuesResponse(
    int ApproachingVacationCap,
    int ExcludedUsers,
    int FacultyAtVacationCap,
    int MissingEmails,
    int PendingRequests);

public sealed record AdminRequestStatusResponse(
    RequestSourceStatusResponse BySource,
    IReadOnlyDictionary<string, int> ByType,
    int Pending,
    int Total);

public sealed record RequestSourceStatusResponse(int Cognos, int Manual);
public sealed record AdminUserStatusResponse(int Admins, int AyFaculty, int Caos, int Chairs, int FyFaculty, int Total);
