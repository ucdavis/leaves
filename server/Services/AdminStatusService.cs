using Microsoft.Extensions.Options;

namespace Server.Services;

public sealed class AdminStatusService
{
    private readonly AdminDirectoryDataService _directoryDataService;
    private readonly AdminStatusDataService _statusDataService;
    private readonly IUniversityHolidayCache _holidayCache;
    private readonly UcDavisHolidayOptions _holidayOptions;

    public AdminStatusService(
        AdminDirectoryDataService directoryDataService,
        AdminStatusDataService statusDataService,
        IUniversityHolidayCache holidayCache,
        IOptions<UcDavisHolidayOptions> holidayOptions)
    {
        _directoryDataService = directoryDataService;
        _statusDataService = statusDataService;
        _holidayCache = holidayCache;
        _holidayOptions = holidayOptions.Value;
    }

    public async Task<AdminStatusPageResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryDataService.LoadStatusDirectoryDataAsync(cancellationToken);
        var statusData = await _statusDataService.LoadStatusDataAsync(cancellationToken);

        var vacationRows = statusData.CurrentAccrualBalances
            .Where(row => row.TypeLabel.Contains("Vacation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var lastHolidayCalendarRefreshUtc = _holidayCache.LastSuccessfulRefreshUtc;

        var dataSources = new[]
        {
            new AdminDataSourceResponse(
                "db-people",
                GetPeoplePromotionStatus(statusData.LatestPeoplePromotionAt),
                statusData.LatestPeoplePromotionAt?.ToString("O")),
            new AdminDataSourceResponse(
                "db-accruals",
                statusData.CurrentAccrualBalances.Count > 0 ? "ready" : "planned",
                statusData.LatestAccrualUpdatedAt?.ToString("O")),
            new AdminDataSourceResponse(
                "ucd-holiday-calendar",
                GetHolidayCalendarStatus(lastHolidayCalendarRefreshUtc),
                lastHolidayCalendarRefreshUtc?.ToString("O"),
                _holidayOptions.BaseUrl),
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
                    FacultyAtVacationCap: vacationRows.Count(row => row.CalculatedBal >= row.AccrualLimit),
                    PendingRequests: statusData.PendingRequestCount)));
    }

    private static bool IsAffirmative(string? value)
    {
        return string.Equals(value?.Trim(), "Y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "True", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPeoplePromotionStatus(DateTime? latestPeoplePromotionAt)
    {
        if (!latestPeoplePromotionAt.HasValue)
        {
            return "planned";
        }

        return latestPeoplePromotionAt.Value < DateTime.UtcNow.AddDays(-30)
            ? "deferred"
            : "ready";
    }

    private static string GetHolidayCalendarStatus(DateTime? lastSuccessfulRefreshUtc)
    {
        if (!lastSuccessfulRefreshUtc.HasValue)
        {
            return "planned";
        }

        return lastSuccessfulRefreshUtc.Value < DateTime.UtcNow.AddDays(-30)
            ? "deferred"
            : "ready";
    }
}

public sealed record AdminStatusPageResponse(
    int ClusterCount,
    int ClustersMissingCaos,
    IReadOnlyList<AdminDataSourceResponse> DataSources,
    int DepartmentCount,
    int DepartmentsMissingChairs,
    AdminStatusSnapshotResponse StatusSnapshot);

public sealed record AdminDataSourceResponse(
    string Id,
    string Status,
    string? UpdatedAt,
    string? SourceUrl = null);

public sealed record AdminStatusSnapshotResponse(
    AdminIssuesResponse Issues);

public sealed record AdminIssuesResponse(
    int ApproachingVacationCap,
    int FacultyAtVacationCap,
    int PendingRequests);
