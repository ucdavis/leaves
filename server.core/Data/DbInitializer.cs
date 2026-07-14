using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.Core.Domain;

namespace Server.Core.Data;

public interface IDbInitializer
{
    Task InitializeAsync(bool includeDevSeed, CancellationToken cancellationToken = default);
}

public class DbInitializer : IDbInitializer
{
    private static readonly AppUserSeed[] DevUsers =
    [
        new(DevelopmentSeedData.LocalAdminIamId, DevelopmentSeedData.LocalAdminEntraObjectId.ToString(), DevelopmentSeedData.LocalAdminEmployeeId, DevelopmentSeedData.LocalAdminDisplayName, DevelopmentSeedData.LocalAdminEmail, true, "2026-07-01T15:55:00", "2026-07-08T18:05:00"),
        new(DevelopmentSeedData.LocalRequesterIamId, DevelopmentSeedData.LocalRequesterEntraObjectId.ToString(), DevelopmentSeedData.LocalRequesterEmployeeId, DevelopmentSeedData.LocalRequesterDisplayName, DevelopmentSeedData.LocalRequesterEmail, true, "2026-07-01T15:56:00", "2026-07-08T18:00:00"),
        new(DevelopmentSeedData.LocalUnauthorizedIamId, DevelopmentSeedData.LocalUnauthorizedEntraObjectId.ToString(), DevelopmentSeedData.LocalUnauthorizedEmployeeId, DevelopmentSeedData.LocalUnauthorizedDisplayName, DevelopmentSeedData.LocalUnauthorizedEmail, true, "2026-07-01T15:57:00", "2026-07-08T17:55:00"),
        new("adminherd", "11111111-1111-1111-1111-111111111111", "84726195", "Maya Thompson", "adminherd@fake.ucdavis.edu", true, "2026-07-01T16:00:00", "2026-07-08T18:10:00"),
        new("apatel", "22222222-2222-2222-2222-222222222222", "36190428", "Asha Patel", "apatel@fake.ucdavis.edu", true, "2026-07-01T16:15:00", "2026-07-08T17:42:00"),
        new("jlin", "33333333-3333-3333-3333-333333333333", "59281746", "Jordan Lin", "jlin@fake.ucdavis.edu", true, "2026-07-01T16:20:00", "2026-07-08T17:35:00"),
        new("egarcia", "44444444-4444-4444-4444-444444444444", "11846372", "Elena Garcia", "egarcia@fake.ucdavis.edu", true, "2026-07-01T16:25:00", "2026-07-08T16:48:00"),
        new("kchen", "55555555-5555-5555-5555-555555555555", "73029514", "Kai Chen", "kchen@fake.ucdavis.edu", true, "2026-07-01T16:30:00", "2026-07-08T19:03:00"),
        new("mowens", "66666666-6666-6666-6666-666666666666", "28465091", "Morgan Owens", "mowens@fake.ucdavis.edu", true, "2026-07-01T16:35:00", "2026-07-08T18:21:00"),
        new("lwilson", "77777777-7777-7777-7777-777777777777", "66510837", "Lena Wilson", "lwilson@fake.ucdavis.edu", true, "2026-07-01T16:40:00", "2026-07-07T15:16:00"),
        new("rshah", "88888888-8888-8888-8888-888888888888", "40957263", "Riya Shah", null, true, "2026-07-01T16:45:00", "2026-07-08T14:10:00"),
        new("nroberts", "99999999-9999-9999-9999-999999999999", "95374128", "Noah Roberts", "nroberts@fake.ucdavis.edu", true, "2026-07-01T16:50:00", "2026-07-08T13:28:00"),
        new("sbaker", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "17628405", "Sofia Baker", "sbaker@fake.ucdavis.edu", true, "2026-07-01T16:55:00", "2026-07-08T11:47:00"),
        new("tnguyen", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "52893617", "Theo Nguyen", "tnguyen@fake.ucdavis.edu", false, "2026-07-01T17:00:00", "2026-07-05T10:12:00"),
    ];

    private static readonly ClusterSeed[] DevClusters =
    [
        new("Animal Sciences Cluster", true, "adminherd"),
        new("Land & Environment Cluster", true, "adminherd"),
    ];

    private static readonly DepartmentSeed[] DevDepartments =
    [
        new("D8K4M1", "Animal Science", 3, "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, true, "2026-07-08T08:00:00"),
        new("P3X7Q9", "Population Health & Reproduction", 3, "Animal Sciences Cluster", WorkflowMode.DirectSubmission, true, "2026-07-08T08:00:00"),
        new("L6V2R5", "Plant Sciences", 3, "Land & Environment Cluster", WorkflowMode.ApprovalRequired, true, "2026-07-08T08:00:00"),
        new("A1N8T4", "Agricultural Experiment Stations", 2, null, WorkflowMode.DirectSubmission, true, "2026-07-08T08:00:00"),
    ];

    private static readonly DepartmentEmailRoutingSeed[] DevDepartmentEmailRoutings =
    [
        new("D8K4M1", "animal-routing@fake.ucdavis.edu", true, "adminherd"),
        new("D8K4M1", "leave-ops@fake.ucdavis.edu", true, "adminherd"),
        new("P3X7Q9", "vet-routing@fake.ucdavis.edu", true, "adminherd"),
        new("L6V2R5", "plants-routing@fake.ucdavis.edu", true, "adminherd"),
    ];

    private static readonly LeaveTypeSeed[] DevLeaveTypes =
    [
        new("Vacation", 10, "Vacation", true, true),
        new("Sick", 20, "Sick Leave", true, true),
        new("FamilyCare", 30, "Family Care Leave", false, true),
        new("Sabbatical", 40, "Sabbatical", false, true),
        new("CompTime", 50, "Compensatory Time", true, true),
    ];

    private static readonly LeaveRequestSeed[] DevLeaveRequests =
    [
        new("lwilson", "66510837", "Vacation", null, LeaveRequestStatus.PendingApproval, "2026-07-14", "2026-07-16", 24.00m, "Summer conference travel.", "Lecture coverage arranged with department staff.", "D8K4M1", "Animal Science", "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, "2026-07-08T15:30:00"),
        new("rshah", "40957263", "FamilyCare", null, LeaveRequestStatus.PendingApproval, "2026-07-21", "2026-07-22", 16.00m, "Family care coverage needed for two half-days.", "Classes shifted to asynchronous materials.", "D8K4M1", "Animal Science", "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, "2026-07-08T16:10:00"),
        new("nroberts", "95374128", "Sick", null, LeaveRequestStatus.Approved, "2026-07-10", "2026-07-10", 8.00m, "Medical appointment.", "Clinic rotation covered by faculty peer.", "P3X7Q9", "Population Health & Reproduction", "Animal Sciences Cluster", WorkflowMode.DirectSubmission, "2026-07-07T09:20:00"),
        new("sbaker", "17628405", "Vacation", "CompTime", LeaveRequestStatus.Approved, "2026-08-03", "2026-08-07", 40.00m, "Planned vacation.", "Greenhouse support assigned to backup specialist.", "L6V2R5", "Plant Sciences", "Land & Environment Cluster", WorkflowMode.ApprovalRequired, "2026-07-01T11:05:00"),
        new("tnguyen", "52893617", "Sabbatical", null, LeaveRequestStatus.Denied, "2026-09-14", "2026-09-18", 40.00m, "Requested study leave before reactivation.", "No approved coverage available for requested period.", "A1N8T4", "Agricultural Experiment Stations", null, WorkflowMode.DirectSubmission, "2026-07-02T10:40:00"),
        new("apatel", "36190428", "Vacation", null, LeaveRequestStatus.Approved, "2026-07-25", "2026-07-25", 8.00m, "Personal day.", "Chair duties delegated to interim reviewer.", "D8K4M1", "Animal Science", "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, "2026-07-03T13:50:00"),
    ];

    private static readonly LeaveRequestActionSeed[] DevLeaveRequestActions =
    [
        new("nroberts", "2026-07-10", "2026-07-10", LeaveRequestActionType.Approved, "egarcia", "2026-07-07T11:10:00", "Approved with same-day coverage confirmed.", null, false),
        new("sbaker", "2026-08-03", "2026-08-07", LeaveRequestActionType.Approved, "kchen", "2026-07-02T09:15:00", "Approved during summer planning review.", null, false),
        new("tnguyen", "2026-09-14", "2026-09-18", LeaveRequestActionType.Denied, "adminherd", "2026-07-02T16:25:00", "Denied until employee returns to active status.", "INACTIVE_EMPLOYEE", false),
        new("apatel", "2026-07-25", "2026-07-25", LeaveRequestActionType.Approved, "adminherd", "2026-07-03T15:00:00", "Administrative leave entry approved.", null, false),
    ];

    private readonly AppDbContext _db;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(AppDbContext db, ILogger<DbInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync(bool includeDevSeed, CancellationToken cancellationToken = default)
    {
        if (_db.Database.IsRelational())
        {
            _logger.LogInformation("Applying database migrations...");
            await _db.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Migrations applied.");
        }
        else
        {
            _logger.LogInformation("Ensuring database is created for provider {ProviderName}...", _db.Database.ProviderName);
            await _db.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("Database ensured.");
        }

        if (includeDevSeed)
        {
            await SeedDevelopmentAsync(cancellationToken);
        }
        else
        {
            await SeedProductionSafeAsync(cancellationToken);
        }
    }

    private async Task SeedDevelopmentAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        await SeedAppUsersAsync(nowUtc, ct);
        var usersByIamId = await LoadUsersByIamIdAsync(ct);

        await SeedAppAdminAssignmentsAsync(usersByIamId, nowUtc, ct);
        await SeedClustersAsync(usersByIamId, nowUtc, ct);

        var clustersByName = await LoadClustersByNameAsync(ct);

        await SeedDepartmentsAsync(clustersByName, nowUtc, ct);
        await SeedDepartmentEmailRoutingsAsync(usersByIamId, nowUtc, ct);
        await SeedLeaveTypesAsync(ct);

        var leaveTypesByKey = await LoadLeaveTypesByKeyAsync(ct);

        await SeedLeaveRequestsAsync(usersByIamId, clustersByName, leaveTypesByKey, nowUtc, ct);

        var leaveRequestsByKey = await LoadLeaveRequestsByKeyAsync(ct);

        await SeedLeaveRequestActionsAsync(usersByIamId, leaveRequestsByKey, ct);
    }

    private async Task SeedAppUsersAsync(DateTime nowUtc, CancellationToken ct)
    {
        var existingIamIds = await _db.AppUsers
            .Select(user => user.IamId)
            .ToListAsync(ct);

        var existing = existingIamIds
            .Select(NormalizeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingUsers = DevUsers
            .Where(user => !existing.Contains(user.IamId))
            .Select(user => new AppUser
            {
                EntraObjectId = Guid.Parse(user.EntraObjectId),
                IamId = user.IamId,
                EmployeeId = user.EmployeeId,
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsActive = user.IsActive,
                FirstLoginUtc = ParseUtc(user.FirstLoginUtc),
                LastLoginUtc = ParseUtc(user.LastLoginUtc),
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc,
            })
            .ToArray();

        if (missingUsers.Length == 0)
        {
            return;
        }

        await _db.AppUsers.AddRangeAsync(missingUsers, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development AppUser rows.", missingUsers.Length);
    }

    private async Task SeedAppAdminAssignmentsAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingAdminIamIds = await _db.AppAdminAssignments
            .Select(assignment => assignment.IamId)
            .ToListAsync(ct);

        var existing = existingAdminIamIds
            .Select(NormalizeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingAssignments = new[] { "adminherd", DevelopmentSeedData.LocalAdminIamId }
            .Where(iamId => !existing.Contains(iamId))
            .Select(iamId => new AppAdminAssignment
            {
                IamId = iamId,
                CreatedByAppUserId = usersByIamId["adminherd"].Id,
                CreatedUtc = nowUtc,
            })
            .ToArray();

        if (missingAssignments.Length == 0)
        {
            return;
        }

        await _db.AppAdminAssignments.AddRangeAsync(missingAssignments, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development AppAdminAssignment rows.", missingAssignments.Length);
    }

    private async Task SeedClustersAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingNames = await _db.Clusters
            .Select(cluster => cluster.ClusterName)
            .ToListAsync(ct);

        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingClusters = DevClusters
            .Where(cluster => !existing.Contains(cluster.ClusterName))
            .Select(cluster => new Cluster
            {
                ClusterName = cluster.ClusterName,
                IsActive = cluster.IsActive,
                CreatedByAppUserId = usersByIamId[cluster.CreatedByIamId].Id,
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc,
            })
            .ToArray();

        if (missingClusters.Length == 0)
        {
            return;
        }

        await _db.Clusters.AddRangeAsync(missingClusters, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development Cluster rows.", missingClusters.Length);
    }

    private async Task SeedDepartmentsAsync(
        IReadOnlyDictionary<string, Cluster> clustersByName,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingCodes = await _db.Departments
            .Select(department => department.DepartmentCode)
            .ToListAsync(ct);

        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingDepartments = DevDepartments
            .Where(department => !existing.Contains(department.DepartmentCode))
            .Select(department => new Department
            {
                DepartmentCode = department.DepartmentCode,
                DepartmentName = department.DepartmentName,
                SourceLevel = department.SourceLevel,
                ClusterId = department.ClusterName is null ? null : clustersByName[department.ClusterName].Id,
                WorkflowMode = department.WorkflowMode,
                IsActive = department.IsActive,
                LastSeenInSourceAt = ParseUtc(department.LastSeenInSourceAt),
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc,
            })
            .ToArray();

        if (missingDepartments.Length == 0)
        {
            return;
        }

        await _db.Departments.AddRangeAsync(missingDepartments, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development Department rows.", missingDepartments.Length);
    }

    private async Task SeedDepartmentEmailRoutingsAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingKeys = await _db.DepartmentEmailRoutings
            .Select(routing => new { routing.DepartmentCode, routing.ToEmail })
            .ToListAsync(ct);

        var existing = existingKeys
            .Select(routing => CreateDepartmentRoutingKey(routing.DepartmentCode, routing.ToEmail))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingRoutings = DevDepartmentEmailRoutings
            .Where(routing => !existing.Contains(CreateDepartmentRoutingKey(routing.DepartmentCode, routing.ToEmail)))
            .Select(routing => new DepartmentEmailRouting
            {
                DepartmentCode = routing.DepartmentCode,
                ToEmail = routing.ToEmail,
                IsActive = routing.IsActive,
                UpdatedByAppUserId = usersByIamId[routing.UpdatedByIamId].Id,
                UpdatedUtc = nowUtc,
            })
            .ToArray();

        if (missingRoutings.Length == 0)
        {
            return;
        }

        await _db.DepartmentEmailRoutings.AddRangeAsync(missingRoutings, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development DepartmentEmailRouting rows.", missingRoutings.Length);
    }

    private async Task SeedLeaveTypesAsync(CancellationToken ct)
    {
        var existingKeys = await _db.LeaveTypes
            .Select(leaveType => leaveType.LeaveTypeKey)
            .ToListAsync(ct);

        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingLeaveTypes = DevLeaveTypes
            .Where(leaveType => !existing.Contains(leaveType.LeaveTypeKey))
            .Select(leaveType => new LeaveType
            {
                LeaveTypeKey = leaveType.LeaveTypeKey,
                SourceLeaveTypeNumber = leaveType.SourceLeaveTypeNumber,
                DisplayName = leaveType.DisplayName,
                HasAccrualBalance = leaveType.HasAccrualBalance,
                IsActive = leaveType.IsActive,
            })
            .ToArray();

        if (missingLeaveTypes.Length == 0)
        {
            return;
        }

        await _db.LeaveTypes.AddRangeAsync(missingLeaveTypes, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development LeaveType rows.", missingLeaveTypes.Length);
    }

    private async Task SeedLeaveRequestsAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        IReadOnlyDictionary<string, Cluster> clustersByName,
        IReadOnlyDictionary<string, LeaveType> leaveTypesByKey,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingKeys = await _db.LeaveRequests
            .Select(request => new { request.IamId, request.StartDate, request.EndDate })
            .ToListAsync(ct);

        var existing = existingKeys
            .Select(request => CreateLeaveRequestKey(request.IamId, request.StartDate, request.EndDate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingLeaveRequests = DevLeaveRequests
            .Where(request => !existing.Contains(CreateLeaveRequestKey(request.IamId, ParseDateOnly(request.StartDate), ParseDateOnly(request.EndDate))))
            .Select(request => new LeaveRequest
            {
                AppUserId = usersByIamId[request.IamId].Id,
                IamId = request.IamId,
                EmployeeId = request.EmployeeId,
                LeaveTypeId = leaveTypesByKey[request.LeaveTypeKey].Id,
                PayLeaveTypeId = request.PayLeaveTypeKey is null ? null : leaveTypesByKey[request.PayLeaveTypeKey].Id,
                Status = request.Status,
                StartDate = ParseDateOnly(request.StartDate),
                EndDate = ParseDateOnly(request.EndDate),
                TotalHours = request.TotalHours,
                Note = request.Note,
                CoveragePlan = request.CoveragePlan,
                ReportingDepartmentCodeSnapshot = request.ReportingDepartmentCodeSnapshot,
                ReportingDepartmentNameSnapshot = request.ReportingDepartmentNameSnapshot,
                ClusterIdSnapshot = request.ClusterNameSnapshot is null ? null : clustersByName[request.ClusterNameSnapshot].Id,
                WorkflowModeSnapshot = request.WorkflowModeSnapshot,
                SubmittedAt = ParseUtc(request.SubmittedAt),
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc,
            })
            .ToArray();

        if (missingLeaveRequests.Length == 0)
        {
            return;
        }

        await _db.LeaveRequests.AddRangeAsync(missingLeaveRequests, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development LeaveRequest rows.", missingLeaveRequests.Length);
    }

    private async Task SeedLeaveRequestActionsAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        IReadOnlyDictionary<string, LeaveRequest> leaveRequestsByKey,
        CancellationToken ct)
    {
        var existingLeaveRequestIds = await _db.LeaveRequestActions
            .Select(action => action.LeaveRequestId)
            .ToListAsync(ct);

        var existing = existingLeaveRequestIds.ToHashSet();

        var missingActions = DevLeaveRequestActions
            .Select(action => new
            {
                Seed = action,
                LeaveRequest = leaveRequestsByKey[CreateLeaveRequestKey(action.RequestIamId, ParseDateOnly(action.RequestStartDate), ParseDateOnly(action.RequestEndDate))],
            })
            .Where(x => !existing.Contains(x.LeaveRequest.Id))
            .Select(x => new LeaveRequestAction
            {
                LeaveRequestId = x.LeaveRequest.Id,
                ActionType = x.Seed.ActionType,
                ActorAppUserId = usersByIamId[x.Seed.ActorIamId].Id,
                ActorIamId = x.Seed.ActorIamId,
                ActionAt = ParseUtc(x.Seed.ActionAt),
                Comment = x.Seed.Comment,
                ReasonCode = x.Seed.ReasonCode,
                IsSelfAction = x.Seed.IsSelfAction,
            })
            .ToArray();

        if (missingActions.Length == 0)
        {
            return;
        }

        await _db.LeaveRequestActions.AddRangeAsync(missingActions, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development LeaveRequestAction rows.", missingActions.Length);
    }

    private async Task<Dictionary<string, AppUser>> LoadUsersByIamIdAsync(CancellationToken ct)
    {
        return await _db.AppUsers
            .ToDictionaryAsync(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase, ct);
    }

    private async Task<Dictionary<string, Cluster>> LoadClustersByNameAsync(CancellationToken ct)
    {
        return await _db.Clusters
            .ToDictionaryAsync(cluster => cluster.ClusterName, StringComparer.OrdinalIgnoreCase, ct);
    }

    private async Task<Dictionary<string, LeaveType>> LoadLeaveTypesByKeyAsync(CancellationToken ct)
    {
        return await _db.LeaveTypes
            .ToDictionaryAsync(leaveType => leaveType.LeaveTypeKey, StringComparer.OrdinalIgnoreCase, ct);
    }

    private async Task<Dictionary<string, LeaveRequest>> LoadLeaveRequestsByKeyAsync(CancellationToken ct)
    {
        var requests = await _db.LeaveRequests.ToListAsync(ct);

        return requests.ToDictionary(
            request => CreateLeaveRequestKey(request.IamId, request.StartDate, request.EndDate),
            StringComparer.OrdinalIgnoreCase);
    }

    // Keep the production path explicit, even if currently empty, so startup behavior stays obvious.
    private Task SeedProductionSafeAsync(CancellationToken ct)
        => Task.CompletedTask;

    private static string NormalizeKey(string value) => value.Trim();

    private static string CreateDepartmentRoutingKey(string departmentCode, string toEmail) => $"{departmentCode}|{toEmail}";

    private static string CreateLeaveRequestKey(string iamId, DateOnly startDate, DateOnly endDate)
        => $"{NormalizeKey(iamId)}|{startDate:O}|{endDate:O}";

    private static DateOnly ParseDateOnly(string value) => DateOnly.Parse(value);

    private static DateTime ParseUtc(string value) => DateTime.Parse(
        value,
        null,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    private sealed record AppUserSeed(
        string IamId,
        string EntraObjectId,
        string EmployeeId,
        string DisplayName,
        string? Email,
        bool IsActive,
        string FirstLoginUtc,
        string LastLoginUtc);

    private sealed record ClusterSeed(
        string ClusterName,
        bool IsActive,
        string CreatedByIamId);

    private sealed record DepartmentSeed(
        string DepartmentCode,
        string DepartmentName,
        byte? SourceLevel,
        string? ClusterName,
        WorkflowMode WorkflowMode,
        bool IsActive,
        string LastSeenInSourceAt);

    private sealed record DepartmentEmailRoutingSeed(
        string DepartmentCode,
        string ToEmail,
        bool IsActive,
        string UpdatedByIamId);

    private sealed record LeaveTypeSeed(
        string LeaveTypeKey,
        int? SourceLeaveTypeNumber,
        string DisplayName,
        bool HasAccrualBalance,
        bool IsActive);

    private sealed record LeaveRequestSeed(
        string IamId,
        string EmployeeId,
        string LeaveTypeKey,
        string? PayLeaveTypeKey,
        LeaveRequestStatus Status,
        string StartDate,
        string EndDate,
        decimal TotalHours,
        string? Note,
        string? CoveragePlan,
        string ReportingDepartmentCodeSnapshot,
        string ReportingDepartmentNameSnapshot,
        string? ClusterNameSnapshot,
        WorkflowMode WorkflowModeSnapshot,
        string SubmittedAt);

    private sealed record LeaveRequestActionSeed(
        string RequestIamId,
        string RequestStartDate,
        string RequestEndDate,
        LeaveRequestActionType ActionType,
        string ActorIamId,
        string ActionAt,
        string? Comment,
        string? ReasonCode,
        bool IsSelfAction);
}
