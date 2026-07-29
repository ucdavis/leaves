namespace Server.Services;

public sealed class AdminStatusService
{
    private readonly AdminDirectoryService _directoryService;

    public AdminStatusService(AdminDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    public async Task<AdminStatusPageResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryService.LoadDirectoryDataAsync(cancellationToken);
        var statusData = await _directoryService.LoadStatusDataAsync(cancellationToken);

        var latestPeoplePromotionAt = directoryData.People
            .Select(person => person.PromotedAt)
            .OfType<DateTime>()
            .DefaultIfEmpty()
            .Max();

        var pendingRequests = statusData.LeaveRequests.Count(request => request.Status == Server.Core.Domain.LeaveRequestStatus.PendingApproval);
        var vacationRows = directoryData.LatestAccrualByEmployeeId.Values
            .Where(row => row.TypeLabel.Contains("Vacation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dataSources = new[]
        {
            new AdminDataSourceResponse(
                "db-people",
                "People",
                "Monthly report.",
                GetPeoplePromotionStatus(latestPeoplePromotionAt),
                latestPeoplePromotionAt == default ? null : latestPeoplePromotionAt.ToString("O")),
            new AdminDataSourceResponse(
                "db-accruals",
                "Employee accruals",
                "Bi-weekly report.",
                directoryData.LatestAccrualByEmployeeId.Count > 0 ? "ready" : "planned",
                GetLatestTimestamp(directoryData.LatestAccrualByEmployeeId.Values.Select(row => row.LastUpdated))),
        };

        return new AdminStatusPageResponse(
            ClusterCount: directoryData.Clusters.Count,
            ClustersMissingCaos: directoryData.Clusters.Count(cluster =>
                !directoryData.CurrentCaoAssignmentsByCluster.ContainsKey(cluster.Id)),
            DataSources: dataSources,
            DepartmentCount: directoryData.Departments.Count,
            DepartmentsMissingChairs: directoryData.Departments.Count(department =>
                !directoryData.CurrentChairAssignmentsByDepartment.ContainsKey(department.DepartmentCode.Trim())),
            StatusSnapshot: new AdminStatusSnapshotResponse(
                Issues: new AdminIssuesResponse(
                    ApproachingVacationCap: vacationRows.Count(row => IsAffirmative(row.ApproachingMax)),
                    FacultyAtVacationCap: vacationRows.Count(row => row.HoursOverUnderPolicyMax >= 0),
                    PendingRequests: pendingRequests)));
    }

    private static bool IsAffirmative(string? value)
    {
        return string.Equals(value?.Trim(), "Y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "True", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPeoplePromotionStatus(DateTime latestPeoplePromotionAt)
    {
        if (latestPeoplePromotionAt == default)
        {
            return "planned";
        }

        return latestPeoplePromotionAt < DateTime.UtcNow.AddDays(-30)
            ? "deferred"
            : "ready";
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
}

public sealed record AdminStatusPageResponse(
    int ClusterCount,
    int ClustersMissingCaos,
    IReadOnlyList<AdminDataSourceResponse> DataSources,
    int DepartmentCount,
    int DepartmentsMissingChairs,
    AdminStatusSnapshotResponse StatusSnapshot);

public sealed record AdminDataSourceResponse(string Id, string Label, string Detail, string Status, string? UpdatedAt);

public sealed record AdminStatusSnapshotResponse(
    AdminIssuesResponse Issues);

public sealed record AdminIssuesResponse(
    int ApproachingVacationCap,
    int FacultyAtVacationCap,
    int PendingRequests);
