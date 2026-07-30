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
        var statusData = await _directoryService.LoadStatusDataAsync(cancellationToken);

        var dataSources = new[]
        {
            new AdminDataSourceResponse(
                "db-people",
                "People",
                "Monthly report.",
                GetPeoplePromotionStatus(statusData.LatestPeoplePromotionAt),
                statusData.LatestPeoplePromotionAt?.ToString("O")),
            new AdminDataSourceResponse(
                "db-accruals",
                "Employee accruals",
                "Bi-weekly report.",
                statusData.LatestAccrualCount > 0 ? "ready" : "planned",
                statusData.LatestAccrualUpdatedAt?.ToString("O")),
        };

        return new AdminStatusPageResponse(
            ClusterCount: statusData.ClusterCount,
            ClustersMissingCaos: statusData.ClustersMissingCaos,
            DataSources: dataSources,
            DepartmentCount: statusData.DepartmentCount,
            DepartmentsMissingChairs: statusData.DepartmentsMissingChairs,
            StatusSnapshot: new AdminStatusSnapshotResponse(
                Issues: new AdminIssuesResponse(
                    ApproachingVacationCap: statusData.ApproachingVacationCap,
                    FacultyAtVacationCap: statusData.FacultyAtVacationCap,
                    PendingRequests: statusData.PendingRequests)));
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
