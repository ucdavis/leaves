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
        var directoryData = await LoadAdminDirectoryDataAsync(cancellationToken);
        var directoryResponses = BuildDirectoryResponses(directoryData);

        return new AdminDepartmentsResponse(
            Clusters: directoryResponses.Clusters,
            Departments: directoryResponses.Departments,
            Users: directoryResponses.Users);
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var directoryData = await LoadAdminDirectoryDataAsync(cancellationToken);
        var directoryResponses = BuildDirectoryResponses(directoryData);
        var dashboardData = await LoadDashboardDataAsync(cancellationToken);
        var dashboardResponseData = BuildDashboardResponseData(directoryData, directoryResponses.Users, dashboardData);

        return new AdminDashboardResponse(
            Clusters: directoryResponses.Clusters,
            DataSources: dashboardResponseData.DataSources,
            Departments: directoryResponses.Departments,
            StatusSnapshot: dashboardResponseData.StatusSnapshot,
            Users: directoryResponses.Users);
    }

    private async Task<AdminDirectoryData> LoadAdminDirectoryDataAsync(CancellationToken cancellationToken)
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
            .AsNoTracking()
            .OrderBy(person => person.FullName)
            .ThenBy(person => person.IamId)
            .ToListAsync(cancellationToken);
        var appUsers = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.IamId)
            .ToListAsync(cancellationToken);

        var adminIamIdSet = (await _db.AppAdminAssignments
                .AsNoTracking()
                .Select(assignment => assignment.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentOverridesByIamId = await GetCurrentDepartmentOverridesByIamId(today, cancellationToken);
        var currentChairAssignmentsByDepartment = await GetCurrentChairAssignmentsByDepartment(today, cancellationToken);
        var currentCaoAssignmentsByCluster = await GetCurrentCaoAssignmentsByCluster(today, cancellationToken);
        var latestAccrualByEmployeeId = await GetLatestAccrualByEmployeeId(cancellationToken);

        return new AdminDirectoryData(
            AppUsers: appUsers,
            Clusters: clusters,
            Departments: departments,
            People: people,
            AdminIamIdSet: adminIamIdSet,
            CurrentOverridesByIamId: currentOverridesByIamId,
            CurrentChairAssignmentsByDepartment: currentChairAssignmentsByDepartment,
            CurrentCaoAssignmentsByCluster: currentCaoAssignmentsByCluster,
            LatestAccrualByEmployeeId: latestAccrualByEmployeeId);
    }

    private static AdminDirectoryResponses BuildDirectoryResponses(AdminDirectoryData directoryData)
    {
        var userIdByIamId = BuildUserIdByIamId(directoryData);

        var departmentResponses = BuildDepartmentResponses(
            directoryData.Departments,
            directoryData.CurrentChairAssignmentsByDepartment,
            userIdByIamId);
        var clusterResponses = BuildClusterResponses(
            directoryData.Clusters,
            directoryData.CurrentCaoAssignmentsByCluster,
            userIdByIamId);
        var roleAssignments = BuildRoleAssignments(
            directoryData.AdminIamIdSet,
            directoryData.CurrentChairAssignmentsByDepartment,
            directoryData.CurrentCaoAssignmentsByCluster);
        var userResponses = BuildUserResponses(directoryData, roleAssignments);

        return new AdminDirectoryResponses(
            Clusters: clusterResponses,
            Departments: departmentResponses,
            Users: userResponses);
    }

    private async Task<DashboardData> LoadDashboardDataAsync(CancellationToken cancellationToken)
    {
        var leaveTypes = await _db.LeaveTypes
            .AsNoTracking()
            .ToDictionaryAsync(type => type.Id, cancellationToken);

        var leaveRequests = await _db.LeaveRequests
            .AsNoTracking()
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .ToListAsync(cancellationToken);

        return new DashboardData(
            LeaveTypes: leaveTypes,
            LeaveRequests: leaveRequests);
    }

    private static Dictionary<string, string> BuildUserIdByIamId(AdminDirectoryData directoryData)
    {
        var userIdByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().IamId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var person in directoryData.People)
        {
            var key = NormalizeKey(person.IamId);
            if (!userIdByIamId.ContainsKey(key))
            {
                userIdByIamId[key] = person.IamId.Trim();
            }
        }

        return userIdByIamId;
    }

    private static IReadOnlyList<AdminDepartmentResponse> BuildDepartmentResponses(
        IEnumerable<Department> departments,
        IReadOnlyDictionary<string, DepartmentChairAssignment> currentChairAssignmentsByDepartment,
        IReadOnlyDictionary<string, string> userIdByIamId)
    {
        return departments
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
    }

    private static IReadOnlyList<AdminClusterResponse> BuildClusterResponses(
        IEnumerable<Cluster> clusters,
        IReadOnlyDictionary<int, ClusterCaoAssignment> currentCaoAssignmentsByCluster,
        IReadOnlyDictionary<string, string> userIdByIamId)
    {
        return clusters
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
    }

    private static RoleAssignments BuildRoleAssignments(
        IReadOnlySet<string> adminIamIdSet,
        IReadOnlyDictionary<string, DepartmentChairAssignment> currentChairAssignmentsByDepartment,
        IReadOnlyDictionary<int, ClusterCaoAssignment> currentCaoAssignmentsByCluster)
    {
        var chairIamIds = currentChairAssignmentsByDepartment.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var caoIamIds = currentCaoAssignmentsByCluster.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new RoleAssignments(
            AdminIamIds: adminIamIdSet,
            ChairIamIds: chairIamIds,
            CaoIamIds: caoIamIds);
    }

    private static IReadOnlyList<AdminUserResponse> BuildUserResponses(
        AdminDirectoryData directoryData,
        RoleAssignments roleAssignments)
    {
        var peopleByIamId = directoryData.People
            .GroupBy(person => NormalizeKey(person.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var appUsersByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return peopleByIamId.Keys
            .OrderBy(key =>
                peopleByIamId.TryGetValue(key, out var person)
                    ? person.FullName ?? person.IamId
                    : key,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(lookupIamId =>
            {
                appUsersByIamId.TryGetValue(lookupIamId, out var appUser);
                peopleByIamId.TryGetValue(lookupIamId, out var person);

                var iamId = person?.IamId.Trim() ?? lookupIamId;
                var employeeId = NormalizeEmployeeId(appUser?.EmployeeId)
                    ?? NormalizeEmployeeId(person?.EmployeeId);
                directoryData.LatestAccrualByEmployeeId.TryGetValue(employeeId ?? string.Empty, out var latestAccrual);
                directoryData.CurrentOverridesByIamId.TryGetValue(lookupIamId, out var currentOverride);

                var departmentCode = currentOverride?.DepartmentCode.Trim()
                    ?? latestAccrual?.Level5Dept.Trim();
                var role = GetRole(
                    roleAssignments.AdminIamIds.Contains(lookupIamId),
                    roleAssignments.ChairIamIds.Contains(lookupIamId),
                    roleAssignments.CaoIamIds.Contains(lookupIamId));

                return new AdminUserResponse(
                    Id: iamId,
                    Active: appUser?.IsActive ?? true,
                    DepartmentId: departmentCode,
                    DepartmentOverrideEndDate: currentOverride?.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    DepartmentOverrideId: currentOverride?.DepartmentCode,
                    DepartmentOverrideStartDate: currentOverride?.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    Designation: GetDesignation(role, person, latestAccrual),
                    Email: appUser?.Email ?? person?.Email ?? string.Empty,
                    EmployeeId: employeeId ?? string.Empty,
                    HasAppUser: appUser != null,
                    IamId: iamId,
                    Name: appUser?.DisplayName ?? person?.FullName ?? iamId,
                    Position: latestAccrual?.JobCodeDescription ?? string.Empty,
                    Role: role);
            })
            .ToList();
    }

    private static DashboardResponseData BuildDashboardResponseData(
        AdminDirectoryData directoryData,
        IReadOnlyList<AdminUserResponse> userResponses,
        DashboardData dashboardData)
    {
        var requestsByType = dashboardData.LeaveRequests
            .GroupBy(request => dashboardData.LeaveTypes.TryGetValue(request.LeaveTypeId, out var leaveType) ? leaveType.DisplayName : "Unknown")
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        var pendingRequests = dashboardData.LeaveRequests.Count(request => request.Status == LeaveRequestStatus.PendingApproval);
        var vacationRows = directoryData.LatestAccrualByEmployeeId.Values
            .Where(row => row.TypeLabel.Contains("Vacation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dataSources = new[]
        {
            new AdminDataSourceResponse("db-people", "People", "Sourced from People records, with app-owned exclude state, role assignments, and department overrides joined by IAM ID.", "ready", GetLatestTimestamp(directoryData.People.Select(person => person.LastFetchedAt ?? person.ModifyDate ?? person.PromotedAt ?? person.FirstIngestedAt).OfType<DateTime>())),
            new AdminDataSourceResponse("db-departments", "Departments", "Sourced from Department, Cluster, chair, CAO, and routing tables.", "ready", GetLatestTimestamp(directoryData.Departments.Select(department => department.UpdatedUtc))),
            new AdminDataSourceResponse("db-accruals", "Employee accruals", "Sourced from the latest EmployeeAccrualBalances rows for reporting departments and positions.", directoryData.LatestAccrualByEmployeeId.Count > 0 ? "ready" : "planned", GetLatestTimestamp(directoryData.LatestAccrualByEmployeeId.Values.Select(row => row.LastUpdated))),
            new AdminDataSourceResponse("db-requests", "Leave requests", "Sourced from LeaveRequest history snapshots.", dashboardData.LeaveRequests.Count > 0 ? "ready" : "planned", GetLatestTimestamp(dashboardData.LeaveRequests.Select(request => request.UpdatedUtc))),
        };

        var statusSnapshot = new AdminStatusSnapshotResponse(
            Departments: new DepartmentStatusResponse(
                Clustered: directoryData.Departments.Count(department => department.ClusterId.HasValue),
                Total: directoryData.Departments.Count,
                WithFaculty: userResponses
                    .Where(user => user.Role is "faculty" or "chair")
                    .Select(user => user.DepartmentId)
                    .Where(departmentId => !string.IsNullOrWhiteSpace(departmentId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()),
            Issues: new AdminIssuesResponse(
                ApproachingVacationCap: vacationRows.Count(row => IsAffirmative(row.ApproachingMax)),
                ExcludedUsers: userResponses.Count(user => !user.Active),
                FacultyAtVacationCap: vacationRows.Count(row => row.HoursOverUnderPolicyMax >= 0),
                MissingEmails: userResponses.Count(user => string.IsNullOrWhiteSpace(user.Email)),
                PendingRequests: pendingRequests),
            Requests: new AdminRequestStatusResponse(
                BySource: new RequestSourceStatusResponse(
                    Cognos: 0,
                    Manual: dashboardData.LeaveRequests.Count),
                ByType: requestsByType,
                Pending: pendingRequests,
                Total: dashboardData.LeaveRequests.Count),
            Users: new AdminUserStatusResponse(
                Admins: userResponses.Count(user => user.Role == "admin"),
                AyFaculty: userResponses.Count(user => user.Designation == "ay"),
                Caos: userResponses.Count(user => user.Role == "cao"),
                Chairs: userResponses.Count(user => user.Role == "chair"),
                FyFaculty: userResponses.Count(user => user.Designation == "fy"),
                Total: userResponses.Count));

        return new DashboardResponseData(
            DataSources: dataSources,
            StatusSnapshot: statusSnapshot);
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

    private sealed record AdminDirectoryData(
        IReadOnlyList<AppUser> AppUsers,
        IReadOnlyList<Cluster> Clusters,
        IReadOnlyList<Department> Departments,
        IReadOnlyList<Person> People,
        IReadOnlySet<string> AdminIamIdSet,
        IReadOnlyDictionary<string, EmployeeReportingDepartmentOverride> CurrentOverridesByIamId,
        IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
        IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
        IReadOnlyDictionary<string, EmployeeAccrualBalance> LatestAccrualByEmployeeId);

    private sealed record AdminDirectoryResponses(
        IReadOnlyList<AdminClusterResponse> Clusters,
        IReadOnlyList<AdminDepartmentResponse> Departments,
        IReadOnlyList<AdminUserResponse> Users);

    private sealed record DashboardData(
        IReadOnlyDictionary<int, LeaveType> LeaveTypes,
        IReadOnlyList<LeaveRequest> LeaveRequests);

    private sealed record DashboardResponseData(
        IReadOnlyList<AdminDataSourceResponse> DataSources,
        AdminStatusSnapshotResponse StatusSnapshot);

    private sealed record RoleAssignments(
        IReadOnlySet<string> AdminIamIds,
        IReadOnlySet<string> ChairIamIds,
        IReadOnlySet<string> CaoIamIds);
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
    bool HasAppUser,
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
