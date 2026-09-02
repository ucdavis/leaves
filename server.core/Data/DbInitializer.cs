using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Server.Core.Domain;

namespace Server.Core.Data;

public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task InitializeAsync(bool includeDevSeed = false, CancellationToken cancellationToken = default);
}

public class DbInitializer : IDbInitializer
{
    private static readonly AppUserSeed[] DevUsers =
    [
        new(DevelopmentSeedData.LocalAdminIamId, DevelopmentSeedData.LocalAdminEntraObjectId.ToString(), DevelopmentSeedData.LocalAdminEmployeeId, DevelopmentSeedData.LocalAdminDisplayName, DevelopmentSeedData.LocalAdminEmail, true, "2026-07-01T15:55:00", "2026-07-08T18:05:00"),
        new(DevelopmentSeedData.LocalFacultyIamId, DevelopmentSeedData.LocalFacultyEntraObjectId.ToString(), DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyDisplayName, DevelopmentSeedData.LocalFacultyEmail, true, "2026-07-01T15:56:00", "2026-07-08T18:00:00"),
        new(DevelopmentSeedData.LocalChairIamId, DevelopmentSeedData.LocalChairEntraObjectId.ToString(), DevelopmentSeedData.LocalChairEmployeeId, DevelopmentSeedData.LocalChairDisplayName, DevelopmentSeedData.LocalChairEmail, true, "2026-08-21T08:00:00", "2026-08-21T08:00:00"),
        new(DevelopmentSeedData.LocalCaoIamId, DevelopmentSeedData.LocalCaoEntraObjectId.ToString(), DevelopmentSeedData.LocalCaoEmployeeId, DevelopmentSeedData.LocalCaoDisplayName, DevelopmentSeedData.LocalCaoEmail, true, "2026-08-21T08:05:00", "2026-08-21T08:05:00"),
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
        new(DevelopmentSeedData.TestClusterName, true, "adminherd"),
    ];

    private static readonly DepartmentSeed[] DevDepartments =
    [
        new("030045", "ANIMAL SCIENCE", 5, "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, true, "2026-07-08T08:00:00"),
        new("030000", "AGR & ENV SCI DEANS OFFICE", 5, "Land & Environment Cluster", WorkflowMode.DirectSubmission, true, "2026-07-08T08:00:00"),
        new(DevelopmentSeedData.TestDepartmentCode, DevelopmentSeedData.TestDepartmentName, 5, DevelopmentSeedData.TestClusterName, WorkflowMode.ApprovalRequired, true, "2026-08-21T08:10:00"),
    ];

    private static readonly DepartmentEmailRoutingSeed[] DevDepartmentEmailRoutings =
    [
        new("030045", "animal-routing@fake.ucdavis.edu", true, "adminherd"),
        new("030045", "leave-ops@fake.ucdavis.edu", true, "adminherd"),
        new("030000", "deans-routing@fake.ucdavis.edu", true, "adminherd"),
        new("030000", "operations-routing@fake.ucdavis.edu", true, "adminherd"),
    ];

    private static readonly PersonSeed[] DevPeople =
    [
        new(DevelopmentSeedData.LocalAdminIamId, DevelopmentSeedData.LocalAdminEmployeeId, DevelopmentSeedData.LocalAdminDisplayName, "Admin", DevelopmentSeedData.LocalAdminEmail, true, false, false, true, "030045", "2026-07-08T08:00:00"),
        new(DevelopmentSeedData.LocalFacultyIamId, DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyDisplayName, "Faculty", DevelopmentSeedData.LocalFacultyEmail, true, true, false, true, DevelopmentSeedData.TestDepartmentCode, "2026-08-21T08:15:00"),
        new(DevelopmentSeedData.LocalChairIamId, DevelopmentSeedData.LocalChairEmployeeId, DevelopmentSeedData.LocalChairDisplayName, "Chair", DevelopmentSeedData.LocalChairEmail, true, true, false, true, DevelopmentSeedData.TestDepartmentCode, "2026-08-21T08:20:00"),
        new(DevelopmentSeedData.LocalCaoIamId, DevelopmentSeedData.LocalCaoEmployeeId, DevelopmentSeedData.LocalCaoDisplayName, "CAO", DevelopmentSeedData.LocalCaoEmail, true, false, true, true, DevelopmentSeedData.TestDepartmentCode, "2026-08-21T08:25:00"),
        new("adminherd", "84726195", "Maya Thompson", null, "adminherd@fake.ucdavis.edu", true, false, true, true, "030000", "2026-07-08T08:15:00"),
        new("apatel", "36190428", "Asha Patel", null, "apatel@fake.ucdavis.edu", true, false, true, true, "030045", "2026-07-08T08:20:00"),
        new("jlin", "59281746", "Jordan Lin", null, "jlin@fake.ucdavis.edu", true, false, true, true, "030045", "2026-07-08T08:25:00"),
        new("egarcia", "11846372", "Elena Garcia", null, "egarcia@fake.ucdavis.edu", true, false, false, true, "030045", "2026-07-08T08:30:00"),
        new("kchen", "73029514", "Kai Chen", null, "kchen@fake.ucdavis.edu", true, false, false, true, "030000", "2026-07-08T08:35:00"),
        new("mowens", "28465091", "Morgan Owens", null, "mowens@fake.ucdavis.edu", true, false, false, true, "030000", "2026-07-08T08:40:00"),
        new("lwilson", "66510837", "Lena Wilson", null, "lwilson@fake.ucdavis.edu", true, false, true, true, "030045", "2026-07-08T08:45:00"),
        new("rshah", "40957263", "Riya Shah", null, null, true, false, true, true, "030045", "2026-07-08T08:50:00"),
        new("nroberts", "95374128", "Noah Roberts", null, "nroberts@fake.ucdavis.edu", true, false, false, true, "030045", "2026-07-08T08:55:00"),
        new("sbaker", "17628405", "Sofia Baker", null, "sbaker@fake.ucdavis.edu", true, false, false, true, "030000", "2026-07-08T09:00:00"),
        new("tnguyen", "52893617", "Theo Nguyen", null, "tnguyen@fake.ucdavis.edu", true, false, false, true, "030000", "2026-07-08T09:05:00"),
    ];

    private static readonly EmployeeReportingDepartmentOverrideSeed[] DevEmployeeReportingDepartmentOverrides =
    [
        new("sbaker", "030045", "2026-07-01", null, "Temporary reporting line coverage for summer operations.", "adminherd", "2026-07-08T09:20:00", null, null),
        new("tnguyen", "030045", "2026-06-15", "2026-07-15", "Historical override retained for testing closed records.", "adminherd", "2026-07-01T09:20:00", "apatel", "2026-07-15T17:00:00"),
        new(DevelopmentSeedData.LocalFacultyIamId, DevelopmentSeedData.TestDepartmentCode, "2026-08-21", null, "Move test faculty into the seeded test department.", "adminherd", "2026-08-21T08:30:00", null, null),
        new(DevelopmentSeedData.LocalChairIamId, DevelopmentSeedData.TestDepartmentCode, "2026-08-21", null, "Assign the seeded test chair to the test department.", "adminherd", "2026-08-21T08:35:00", null, null),
        new(DevelopmentSeedData.LocalCaoIamId, DevelopmentSeedData.TestDepartmentCode, "2026-08-21", null, "Assign the seeded test CAO to the test department.", "adminherd", "2026-08-21T08:40:00", null, null),
    ];

    private static readonly DepartmentChairAssignmentSeed[] DevDepartmentChairAssignments =
    [
        new("030045", "apatel", "2026-01-01", null, "adminherd", "2026-07-08T09:25:00", null, null),
        new("030045", "jlin", "2026-01-01", null, "adminherd", "2026-07-08T09:26:00", null, null),
        new("030000", "kchen", "2025-09-01", "2026-06-30", "adminherd", "2026-06-01T09:26:00", "apatel", "2026-06-30T17:00:00"),
        new(DevelopmentSeedData.TestDepartmentCode, DevelopmentSeedData.LocalChairIamId, "2026-08-21", null, "adminherd", "2026-08-21T08:45:00", null, null),
    ];

    private static readonly ClusterCaoAssignmentSeed[] DevClusterCaoAssignments =
    [
        new("Animal Sciences Cluster", "adminherd", "2026-01-01", null, "adminherd", "2026-07-08T09:30:00", null, null),
        new("Land & Environment Cluster", "mowens", "2026-01-01", null, "adminherd", "2026-07-08T09:31:00", null, null),
        new(DevelopmentSeedData.TestClusterName, DevelopmentSeedData.LocalCaoIamId, "2026-08-21", null, "adminherd", "2026-08-21T08:50:00", null, null),
    ];

    private static readonly LeaveTypeSeed[] DevLeaveTypes =
    [
        new("Vacation", 10, "Vacation", true, true),
        new("Sick", 20, "Sick Leave", true, true),
        new("ProfessionalDevelopment", null, "Professional Development", false, true),
        new("FamilyCare", 30, "FMLA", false, true),
        new("Sabbatical", 40, "Sabbatical", false, true),
        new("CompTime", 50, "Compensatory Time", true, true),
    ];

    private static readonly EmployeeAccrualBalanceSeed[] DevEmployeeAccrualBalances =
    [
        // The local faculty persona has two biweekly snapshots so balance-history development has useful data.
        new(DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyEmail, DevelopmentSeedData.LocalFacultyDisplayName, "2026-06-28", "40001234", 10, "Vacation", 88.00m, 0.00m, 8.00m, 0.00m, 96.00m, 240.00m, "FAC", "Faculty", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new(DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyEmail, DevelopmentSeedData.LocalFacultyDisplayName, "2026-07-12", "40001234", 10, "Vacation", 96.00m, 8.00m, 8.00m, 0.00m, 96.00m, 240.00m, "FAC", "Faculty", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new(DevelopmentSeedData.LocalFacultyEmployeeId, DevelopmentSeedData.LocalFacultyEmail, DevelopmentSeedData.LocalFacultyDisplayName, "2026-07-12", "40001234", 20, "Sick Leave", 280.00m, 0.00m, 8.00m, 0.00m, 288.00m, 0.00m, "FAC", "Faculty", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new(DevelopmentSeedData.LocalChairEmployeeId, DevelopmentSeedData.LocalChairEmail, DevelopmentSeedData.LocalChairDisplayName, "2026-07-12", "40001235", 10, "Vacation", 120.00m, 0.00m, 8.00m, 0.00m, 128.00m, 240.00m, "FAC", "Faculty", "001700", "Department Chair", DevelopmentSeedData.TestDepartmentCode, DevelopmentSeedData.TestDepartmentName),
        new(DevelopmentSeedData.LocalCaoEmployeeId, DevelopmentSeedData.LocalCaoEmail, DevelopmentSeedData.LocalCaoDisplayName, "2026-07-12", "40001236", 10, "Vacation", 120.00m, 0.00m, 8.00m, 0.00m, 128.00m, 240.00m, "FAC", "Faculty", "001700", "Chief Administrative Officer", DevelopmentSeedData.TestDepartmentCode, DevelopmentSeedData.TestDepartmentName),

        // Monthly and biweekly employees intentionally have different latest dates.
        new("66510837", "lwilson@fake.ucdavis.edu", "Lena Wilson", "2026-06-30", "40002345", 10, "Vacation", 160.00m, 0.00m, 8.00m, 0.00m, 168.00m, 240.00m, "FAC", "Faculty", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new("66510837", "lwilson@fake.ucdavis.edu", "Lena Wilson", "2026-06-30", "40002345", 20, "Sick Leave", 272.00m, 0.00m, 8.00m, 0.00m, 280.00m, 0.00m, "FAC", "Faculty", "001700", "Professor", "030045", "ANIMAL SCIENCE"),
        new("36190428", "apatel@fake.ucdavis.edu", "Asha Patel", "2026-06-30", "40003456", 10, "Vacation", 210.00m, 8.00m, 10.00m, 0.00m, 212.00m, 240.00m, "MSP", "Managers and Senior Professionals", "000245", "Department Chair", "030045", "ANIMAL SCIENCE"),
        new("17628405", "sbaker@fake.ucdavis.edu", "Sofia Baker", "2026-07-12", "40004567", 50, "Compensatory Time", 18.00m, 0.00m, 2.00m, 0.00m, 20.00m, 80.00m, "PSS", "Professional and Support Staff", "006257", "Agricultural Technician", "030000", "AGR & ENV SCI DEANS OFFICE"),
    ];

    private static readonly LeaveRequestSeed[] DevLeaveRequests =
    [
        new("lwilson", "66510837", "Vacation", null, LeaveRequestStatus.PendingApproval, "2026-07-14", "2026-07-16", 24.00m, "Summer conference travel.", "Lecture coverage arranged with department staff.", "030045", "ANIMAL SCIENCE", "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, "2026-07-08T15:30:00"),
        new("rshah", "40957263", "FamilyCare", null, LeaveRequestStatus.PendingApproval, "2026-07-21", "2026-07-22", 16.00m, "Family care coverage needed for two half-days.", "Classes shifted to asynchronous materials.", "030045", "ANIMAL SCIENCE", "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, "2026-07-08T16:10:00"),
        new("nroberts", "95374128", "Sick", null, LeaveRequestStatus.Approved, "2026-07-10", "2026-07-10", 8.00m, "Medical appointment.", "Clinic rotation covered by faculty peer.", "030045", "ANIMAL SCIENCE", "Animal Sciences Cluster", WorkflowMode.DirectSubmission, "2026-07-07T09:20:00"),
        new("sbaker", "17628405", "Vacation", "CompTime", LeaveRequestStatus.Approved, "2026-08-03", "2026-08-07", 40.00m, "Planned vacation.", "Greenhouse support assigned to backup specialist.", "030000", "AGR & ENV SCI DEANS OFFICE", "Land & Environment Cluster", WorkflowMode.ApprovalRequired, "2026-07-01T11:05:00"),
        new("tnguyen", "52893617", "Sabbatical", null, LeaveRequestStatus.Denied, "2026-09-14", "2026-09-18", 40.00m, "Requested study leave before reactivation.", "No approved coverage available for requested period.", "030000", "AGR & ENV SCI DEANS OFFICE", "Land & Environment Cluster", WorkflowMode.DirectSubmission, "2026-07-02T10:40:00"),
        new("apatel", "36190428", "Vacation", null, LeaveRequestStatus.Approved, "2026-07-25", "2026-07-25", 8.00m, "Personal day.", "Chair duties delegated to interim reviewer.", "030045", "ANIMAL SCIENCE", "Animal Sciences Cluster", WorkflowMode.ApprovalRequired, "2026-07-03T13:50:00"),
    ];

    private static readonly LeaveRequestActionSeed[] DevLeaveRequestActions =
    [
        new("nroberts", "2026-07-10", "2026-07-10", LeaveRequestActionType.Approved, "egarcia", "2026-07-07T11:10:00", "Approved with same-day coverage confirmed.", null, false),
        new("sbaker", "2026-08-03", "2026-08-07", LeaveRequestActionType.Approved, "kchen", "2026-07-02T09:15:00", "Approved during summer planning review.", null, false),
        new("tnguyen", "2026-09-14", "2026-09-18", LeaveRequestActionType.Denied, "adminherd", "2026-07-02T16:25:00", "Denied until employee returns to active status.", "INACTIVE_EMPLOYEE", false),
        new("apatel", "2026-07-25", "2026-07-25", LeaveRequestActionType.Approved, "adminherd", "2026-07-03T15:00:00", "Administrative leave entry approved.", null, false),
    ];

    private static readonly OutboundMessageSeed[] DevOutboundMessages =
    [
        new("nroberts", "2026-07-10", "2026-07-10", "LeaveRequestApproved", "nroberts@fake.ucdavis.edu", OutboundMessageStatus.Sent, "2026-07-07T11:15:00", "2026-07-07T11:16:00", 1, null, "smtp-approved-001"),
        new("sbaker", "2026-08-03", "2026-08-07", "LeaveRequestApproved", "sbaker@fake.ucdavis.edu", OutboundMessageStatus.Pending, "2026-07-02T09:20:00", null, 0, null, null),
        new("tnguyen", "2026-09-14", "2026-09-18", "LeaveRequestDenied", "tnguyen@fake.ucdavis.edu", OutboundMessageStatus.Failed, "2026-07-02T16:30:00", null, 2, "SMTP timeout during sandbox test send.", null),
    ];

    private readonly AppDbContext _db;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(AppDbContext db, ILogger<DbInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        InitializeAsync(includeDevSeed: false, cancellationToken: cancellationToken);

    public async Task InitializeAsync(bool includeDevSeed = false, CancellationToken cancellationToken = default)
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
    }

    private async Task SeedDevelopmentAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        await SeedAppUsersAsync(nowUtc, ct);
        var usersByIamId = await LoadUsersByIamIdAsync(ct);
        await SeedPeopleAsync(usersByIamId, ct);

        await SeedAppAdminAssignmentsAsync(usersByIamId, nowUtc, ct);
        await SeedClustersAsync(usersByIamId, nowUtc, ct);

        var clustersByName = await LoadClustersByNameAsync(ct);

        await SeedDepartmentsAsync(clustersByName, nowUtc, ct);
        await SeedEmployeeReportingDepartmentOverridesAsync(usersByIamId, nowUtc, ct);
        await SeedDepartmentChairAssignmentsAsync(usersByIamId, nowUtc, ct);
        await SeedClusterCaoAssignmentsAsync(usersByIamId, clustersByName, nowUtc, ct);
        await SeedDepartmentEmailRoutingsAsync(usersByIamId, nowUtc, ct);
        await SeedLeaveTypesAsync(ct);
        await SeedEmployeeAccrualBalancesAsync(ct);

        var leaveTypesByKey = await LoadLeaveTypesByKeyAsync(ct);

        await SeedLeaveRequestsAsync(usersByIamId, clustersByName, leaveTypesByKey, nowUtc, ct);

        var leaveRequestsByKey = await LoadLeaveRequestsByKeyAsync(ct);

        await SeedLeaveRequestDaysAsync(leaveRequestsByKey, ct);
        await SeedLeaveRequestActionsAsync(usersByIamId, leaveRequestsByKey, ct);
        await SeedOutboundMessagesAsync(leaveRequestsByKey, ct);
    }

    private async Task SeedPeopleAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        CancellationToken ct)
    {
        var existingIamIds = await _db.Set<Person>()
            .Select(person => person.IamId)
            .ToListAsync(ct);

        var existing = existingIamIds
            .Select(NormalizeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingPeople = DevPeople
            .Where(person => !existing.Contains(person.IamId))
            .Select(person => CreatePerson(person, usersByIamId))
            .ToArray();

        if (missingPeople.Length == 0)
        {
            return;
        }

        await _db.Set<Person>().AddRangeAsync(missingPeople, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development People rows.", missingPeople.Length);
    }

    private async Task SeedAppUsersAsync(DateTime nowUtc, CancellationToken ct)
    {
        var existingUsers = await _db.AppUsers.ToListAsync(ct);
        var newUsers = new List<AppUser>();

        foreach (var seed in DevUsers)
        {
            var user = FindExistingUser(existingUsers, seed);
            if (user == null)
            {
                newUsers.Add(CreateAppUser(seed, nowUtc));
                continue;
            }

            ApplyAppUserSeed(user, seed, nowUtc);
        }

        if (newUsers.Count > 0)
        {
            await _db.AppUsers.AddRangeAsync(newUsers, ct);
        }

        if (newUsers.Count == 0 && existingUsers.Count == 0)
        {
            return;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded or updated development AppUser rows.");
    }

    private static AppUser? FindExistingUser(
        IReadOnlyCollection<AppUser> existingUsers,
        AppUserSeed seed)
    {
        var normalizedIamId = NormalizeKey(seed.IamId);
        var normalizedEmployeeId = NormalizeKey(seed.EmployeeId);

        var userByIamId = existingUsers.FirstOrDefault(user =>
            NormalizeKey(user.IamId) == normalizedIamId);
        if (userByIamId != null)
        {
            return userByIamId;
        }

        if (string.IsNullOrWhiteSpace(normalizedEmployeeId))
        {
            return null;
        }

        return existingUsers.FirstOrDefault(user =>
            !string.IsNullOrWhiteSpace(user.EmployeeId) &&
            NormalizeKey(user.EmployeeId) == normalizedEmployeeId);
    }

    private static AppUser CreateAppUser(AppUserSeed seed, DateTime nowUtc)
    {
        return new AppUser
        {
            EntraObjectId = Guid.Parse(seed.EntraObjectId),
            IamId = seed.IamId,
            EmployeeId = seed.EmployeeId,
            DisplayName = seed.DisplayName,
            Email = seed.Email,
            IsActive = seed.IsActive,
            FirstLoginUtc = ParseUtc(seed.FirstLoginUtc),
            LastLoginUtc = ParseUtc(seed.LastLoginUtc),
            CreatedUtc = nowUtc,
            UpdatedUtc = nowUtc,
        };
    }

    private static void ApplyAppUserSeed(AppUser user, AppUserSeed seed, DateTime nowUtc)
    {
        user.EntraObjectId = Guid.Parse(seed.EntraObjectId);
        user.IamId = seed.IamId;
        user.EmployeeId = seed.EmployeeId;
        user.DisplayName = seed.DisplayName;
        user.Email = seed.Email;
        user.IsActive = seed.IsActive;
        user.FirstLoginUtc = ParseUtc(seed.FirstLoginUtc);
        user.LastLoginUtc = ParseUtc(seed.LastLoginUtc);
        user.UpdatedUtc = nowUtc;
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

    private async Task SeedEmployeeReportingDepartmentOverridesAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingKeys = await _db.EmployeeReportingDepartmentOverrides
            .Select(overrideRow => new { overrideRow.IamId, overrideRow.EffectiveStartDate })
            .ToListAsync(ct);

        var existing = existingKeys
            .Select(overrideRow => CreateEffectiveRangeKey(overrideRow.IamId, overrideRow.EffectiveStartDate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingOverrides = DevEmployeeReportingDepartmentOverrides
            .Where(overrideRow => !existing.Contains(CreateEffectiveRangeKey(overrideRow.IamId, ParseDateOnly(overrideRow.EffectiveStartDate))))
            .Select(overrideRow => new EmployeeReportingDepartmentOverride
            {
                IamId = overrideRow.IamId,
                DepartmentCode = overrideRow.DepartmentCode,
                EffectiveStartDate = ParseDateOnly(overrideRow.EffectiveStartDate),
                EffectiveEndDateExclusive = overrideRow.EffectiveEndDateExclusive is null ? null : ParseDateOnly(overrideRow.EffectiveEndDateExclusive),
                Reason = overrideRow.Reason,
                CreatedByAppUserId = usersByIamId[overrideRow.CreatedByIamId].Id,
                CreatedUtc = ParseUtc(overrideRow.CreatedUtc),
                ClosedByAppUserId = overrideRow.ClosedByIamId is null ? null : usersByIamId[overrideRow.ClosedByIamId].Id,
                ClosedUtc = overrideRow.ClosedUtc is null ? null : ParseUtc(overrideRow.ClosedUtc),
            })
            .ToArray();

        if (missingOverrides.Length == 0)
        {
            return;
        }

        await _db.EmployeeReportingDepartmentOverrides.AddRangeAsync(missingOverrides, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development EmployeeReportingDepartmentOverride rows.", missingOverrides.Length);
    }

    private async Task SeedDepartmentChairAssignmentsAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingKeys = await _db.DepartmentChairAssignments
            .Select(assignment => new { assignment.DepartmentCode, assignment.IamId, assignment.EffectiveStartDate })
            .ToListAsync(ct);

        var existing = existingKeys
            .Select(assignment => CreateAssignmentKey(assignment.DepartmentCode, assignment.IamId, assignment.EffectiveStartDate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingAssignments = DevDepartmentChairAssignments
            .Where(assignment => !existing.Contains(CreateAssignmentKey(
                assignment.DepartmentCode,
                assignment.IamId,
                ParseDateOnly(assignment.EffectiveStartDate))))
            .Select(assignment => new DepartmentChairAssignment
            {
                DepartmentCode = assignment.DepartmentCode,
                IamId = assignment.IamId,
                EffectiveStartDate = ParseDateOnly(assignment.EffectiveStartDate),
                EffectiveEndDateExclusive = assignment.EffectiveEndDateExclusive is null ? null : ParseDateOnly(assignment.EffectiveEndDateExclusive),
                CreatedByAppUserId = usersByIamId[assignment.CreatedByIamId].Id,
                CreatedUtc = ParseUtc(assignment.CreatedUtc),
                ClosedByAppUserId = assignment.ClosedByIamId is null ? null : usersByIamId[assignment.ClosedByIamId].Id,
                ClosedUtc = assignment.ClosedUtc is null ? null : ParseUtc(assignment.ClosedUtc),
            })
            .ToArray();

        if (missingAssignments.Length == 0)
        {
            return;
        }

        await _db.DepartmentChairAssignments.AddRangeAsync(missingAssignments, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development DepartmentChairAssignment rows.", missingAssignments.Length);
    }

    private async Task SeedClusterCaoAssignmentsAsync(
        IReadOnlyDictionary<string, AppUser> usersByIamId,
        IReadOnlyDictionary<string, Cluster> clustersByName,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var existingKeys = await _db.ClusterCaoAssignments
            .Select(assignment => new { assignment.ClusterId, assignment.IamId, assignment.EffectiveStartDate })
            .ToListAsync(ct);

        var existing = existingKeys
            .Select(assignment => CreateAssignmentKey(assignment.ClusterId.ToString(), assignment.IamId, assignment.EffectiveStartDate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingAssignments = DevClusterCaoAssignments
            .Where(assignment =>
            {
                var clusterId = clustersByName[assignment.ClusterName].Id;
                return !existing.Contains(CreateAssignmentKey(clusterId.ToString(), assignment.IamId, ParseDateOnly(assignment.EffectiveStartDate)));
            })
            .Select(assignment => new ClusterCaoAssignment
            {
                ClusterId = clustersByName[assignment.ClusterName].Id,
                IamId = assignment.IamId,
                EffectiveStartDate = ParseDateOnly(assignment.EffectiveStartDate),
                EffectiveEndDateExclusive = assignment.EffectiveEndDateExclusive is null ? null : ParseDateOnly(assignment.EffectiveEndDateExclusive),
                CreatedByAppUserId = usersByIamId[assignment.CreatedByIamId].Id,
                CreatedUtc = ParseUtc(assignment.CreatedUtc),
                ClosedByAppUserId = assignment.ClosedByIamId is null ? null : usersByIamId[assignment.ClosedByIamId].Id,
                ClosedUtc = assignment.ClosedUtc is null ? null : ParseUtc(assignment.ClosedUtc),
            })
            .ToArray();

        if (missingAssignments.Length == 0)
        {
            return;
        }

        await _db.ClusterCaoAssignments.AddRangeAsync(missingAssignments, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development ClusterCaoAssignment rows.", missingAssignments.Length);
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
        var existingLeaveTypes = await _db.LeaveTypes
            .ToListAsync(ct);

        var existingByKey = existingLeaveTypes.ToDictionary(
            leaveType => leaveType.LeaveTypeKey,
            StringComparer.OrdinalIgnoreCase);

        var updated = false;

        foreach (var seed in DevLeaveTypes)
        {
            if (!existingByKey.TryGetValue(seed.LeaveTypeKey, out var existingLeaveType))
            {
                continue;
            }

            if (existingLeaveType.SourceLeaveTypeNumber != seed.SourceLeaveTypeNumber)
            {
                existingLeaveType.SourceLeaveTypeNumber = seed.SourceLeaveTypeNumber;
                updated = true;
            }

            if (!string.Equals(existingLeaveType.DisplayName, seed.DisplayName, StringComparison.Ordinal))
            {
                existingLeaveType.DisplayName = seed.DisplayName;
                updated = true;
            }

            if (existingLeaveType.HasAccrualBalance != seed.HasAccrualBalance)
            {
                existingLeaveType.HasAccrualBalance = seed.HasAccrualBalance;
                updated = true;
            }

            if (existingLeaveType.IsActive != seed.IsActive)
            {
                existingLeaveType.IsActive = seed.IsActive;
                updated = true;
            }
        }

        if (updated)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Updated existing development LeaveType rows.");
        }

        var missingLeaveTypes = DevLeaveTypes
            .Where(leaveType => !existingByKey.ContainsKey(leaveType.LeaveTypeKey))
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

    private async Task SeedEmployeeAccrualBalancesAsync(CancellationToken ct)
    {
        var seededEmployeeIds = DevEmployeeAccrualBalances
            .Select(balance => balance.EmployeeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingBalances = await _db.EmployeeAccrualBalances
            .Where(balance => seededEmployeeIds.Contains(balance.EmployeeId))
            .ToListAsync(ct);

        var existingByKey = existingBalances
            .ToDictionary(balance => CreateEmployeeAccrualBalanceKey(
                balance.EmployeeId,
                balance.AsOfDate,
                balance.PositionNumber,
                balance.LeaveTypeNumber), StringComparer.OrdinalIgnoreCase);

        var missingBalances = new List<EmployeeAccrualBalance>();
        var updatedCount = 0;
        foreach (var seed in DevEmployeeAccrualBalances)
        {
            var key = CreateEmployeeAccrualBalanceKey(
                seed.EmployeeId,
                ParseDateOnly(seed.AsOfDate),
                seed.PositionNumber,
                seed.LeaveTypeNumber);

            if (existingByKey.TryGetValue(key, out var existingBalance))
            {
                if (existingBalance.EmployeeEmail != seed.EmployeeEmail ||
                    existingBalance.EmployeeName != seed.EmployeeName)
                {
                    existingBalance.EmployeeEmail = seed.EmployeeEmail;
                    existingBalance.EmployeeName = seed.EmployeeName;
                    updatedCount++;
                }

                continue;
            }

            missingBalances.Add(CreateEmployeeAccrualBalance(seed));
        }

        if (missingBalances.Count == 0 && updatedCount == 0)
        {
            return;
        }

        if (missingBalances.Count > 0)
        {
            await _db.Set<EmployeeAccrualBalance>().AddRangeAsync(missingBalances, ct);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {SeededCount} and updated {UpdatedCount} development EmployeeAccrualBalances rows.",
            missingBalances.Count,
            updatedCount);
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

    private async Task SeedLeaveRequestDaysAsync(
        IReadOnlyDictionary<string, LeaveRequest> leaveRequestsByKey,
        CancellationToken ct)
    {
        var existingKeys = await _db.LeaveRequestDays
            .Select(day => new { day.LeaveRequestId, day.LeaveDate })
            .ToListAsync(ct);

        var existing = existingKeys
            .Select(day => CreateLeaveRequestDayKey(day.LeaveRequestId, day.LeaveDate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingDays = DevLeaveRequests
            .SelectMany(request => ExpandLeaveRequestDays(request, leaveRequestsByKey))
            .Where(day => !existing.Contains(CreateLeaveRequestDayKey(day.LeaveRequestId, day.LeaveDate)))
            .ToArray();

        if (missingDays.Length == 0)
        {
            return;
        }

        await _db.LeaveRequestDays.AddRangeAsync(missingDays, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development LeaveRequestDay rows.", missingDays.Length);
    }

    private async Task SeedOutboundMessagesAsync(
        IReadOnlyDictionary<string, LeaveRequest> leaveRequestsByKey,
        CancellationToken ct)
    {
        var existingDedupeKeys = await _db.OutboundMessages
            .Select(message => message.DedupeKey)
            .ToListAsync(ct);

        var existing = existingDedupeKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingMessages = DevOutboundMessages
            .Where(message => !existing.Contains(CreateOutboundMessageDedupeKey(
                message.RequestIamId,
                message.RequestStartDate,
                message.RequestEndDate,
                message.NotificationType,
                message.RecipientEmail)))
            .Select(message =>
            {
                var requestKey = CreateLeaveRequestKey(message.RequestIamId, ParseDateOnly(message.RequestStartDate), ParseDateOnly(message.RequestEndDate));
                return new OutboundMessage
                {
                    LeaveRequestId = leaveRequestsByKey[requestKey].Id,
                    NotificationType = message.NotificationType,
                    RecipientEmail = message.RecipientEmail,
                    Status = message.Status,
                    DedupeKey = CreateOutboundMessageDedupeKey(message.RequestIamId, message.RequestStartDate, message.RequestEndDate, message.NotificationType, message.RecipientEmail),
                    NotBeforeUtc = ParseUtc(message.NotBeforeUtc),
                    LockedUntilUtc = null,
                    LockId = null,
                    AttemptCount = message.AttemptCount,
                    LastError = message.LastError,
                    ProviderMessageId = message.ProviderMessageId,
                    CreatedUtc = ParseUtc(message.NotBeforeUtc),
                    SentUtc = message.SentUtc is null ? null : ParseUtc(message.SentUtc),
                };
            })
            .ToArray();

        if (missingMessages.Length == 0)
        {
            return;
        }

        await _db.OutboundMessages.AddRangeAsync(missingMessages, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} development OutboundMessage rows.", missingMessages.Length);
    }

    private async Task<Dictionary<string, AppUser>> LoadUsersByIamIdAsync(CancellationToken ct)
    {
        return await _db.AppUsers
            .ToDictionaryAsync(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase, ct);
    }

    private async Task<Dictionary<string, Cluster>> LoadClustersByNameAsync(CancellationToken ct)
    {
        var clusters = await _db.Clusters
            .OrderByDescending(cluster => cluster.IsActive)
            .ThenBy(cluster => cluster.Id)
            .ToListAsync(ct);

        return clusters
            .GroupBy(cluster => NormalizeKey(cluster.ClusterName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, LeaveType>> LoadLeaveTypesByKeyAsync(CancellationToken ct)
    {
        return await _db.LeaveTypes
            .ToDictionaryAsync(leaveType => leaveType.LeaveTypeKey, StringComparer.OrdinalIgnoreCase, ct);
    }

    private async Task<Dictionary<string, LeaveRequest>> LoadLeaveRequestsByKeyAsync(CancellationToken ct)
    {
        var requests = await _db.LeaveRequests
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .ToListAsync(ct);

        return requests
            .GroupBy(
                request => CreateLeaveRequestKey(request.IamId, request.StartDate, request.EndDate),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string value) => value.Trim();

    private static string CreateDepartmentRoutingKey(string departmentCode, string toEmail) => $"{departmentCode}|{toEmail}";

    private static string CreateLeaveRequestKey(string iamId, DateOnly startDate, DateOnly endDate)
        => $"{NormalizeKey(iamId)}|{startDate:O}|{endDate:O}";

    private static string CreateEffectiveRangeKey(string iamId, DateOnly effectiveStartDate)
        => $"{NormalizeKey(iamId)}|{effectiveStartDate:O}";

    private static string CreateAssignmentKey(string scopeKey, string iamId, DateOnly effectiveStartDate)
        => $"{NormalizeKey(scopeKey)}|{NormalizeKey(iamId)}|{effectiveStartDate:O}";

    private static string CreateLeaveRequestDayKey(int leaveRequestId, DateOnly leaveDate)
        => $"{leaveRequestId}|{leaveDate:O}";

    private static string CreateEmployeeAccrualBalanceKey(
        string employeeId,
        DateOnly asOfDate,
        string positionNumber,
        int leaveTypeNumber)
        => $"{NormalizeKey(employeeId)}|{asOfDate:O}|{NormalizeKey(positionNumber)}|{leaveTypeNumber}";

    private static string CreateOutboundMessageDedupeKey(
        string iamId,
        string requestStartDate,
        string requestEndDate,
        string notificationType,
        string recipientEmail)
        => $"{NormalizeKey(iamId)}|{requestStartDate}|{requestEndDate}|{notificationType}|{recipientEmail}".ToLowerInvariant();

    private static EmployeeAccrualBalance CreateEmployeeAccrualBalance(EmployeeAccrualBalanceSeed seed)
    {
        var asOfDate = ParseDateOnly(seed.AsOfDate);
        var loadedAt = ParseUtc($"{seed.AsOfDate}T14:00:00");

        return new EmployeeAccrualBalance
        {
            EmployeeId = seed.EmployeeId,
            AsOfDate = asOfDate,
            PositionNumber = seed.PositionNumber,
            LeaveTypeNumber = seed.LeaveTypeNumber,
            EmployeeEmail = seed.EmployeeEmail,
            EmployeeName = seed.EmployeeName,
            UnionCode = "99",
            UnionDescription = "Non-Represented",
            EmployeeClassCode = seed.EmployeeClassCode,
            EmployeeClassDescription = seed.EmployeeClassDescription,
            JobCode = seed.JobCode,
            JobCodeDescription = seed.JobCodeDescription,
            ReportsToPositionNumber = "40000001",
            ReportsToEmployeeId = "84726195",
            ReportsToEmployeeName = "Maya Thompson",
            HrStatus = "A",
            EmployeeStatus = "A",
            EmployeeStatusDescription = "Active",
            EmployeeType = "E",
            EmployeeTypeDescription = "Employee",
            HourlyRateFTE = 1.0000m,
            TypeLabel = seed.TypeLabel,
            PrevBal = seed.PrevBal,
            HoursTaken = seed.HoursTaken,
            AccrualHours = seed.AccrualHours,
            AdjustedHours = seed.AdjustedHours,
            CalculatedBal = seed.CalculatedBal,
            AccrualLimit = seed.AccrualLimit,
            ApproachingMax = seed.AccrualLimit > 0m && seed.CalculatedBal >= seed.AccrualLimit * 0.9m ? "Y" : "N",
            HoursOverUnderPolicyMax = seed.AccrualLimit > 0m ? seed.AccrualLimit - seed.CalculatedBal : 0.00m,
            AccrualPercentage = seed.AccrualLimit > 0m ? decimal.Round(seed.CalculatedBal / seed.AccrualLimit * 100m, 2) : 0.00m,
            ExceptionalMaxVacationOnly = 0,
            Level1Dept = "DVCMP",
            Level1DeptDesc = "UC Davis Campus",
            Level2Dept = "DVCMP",
            Level2DeptDesc = "UC DAVIS CAMPUS",
            Level3Dept = "01",
            Level3DeptDesc = "AGRICULTURE",
            Level4Dept = "S2000",
            Level4DeptDesc = "AGRICULTURE SUBDIV",
            Level5Dept = seed.Level5Dept,
            Level5DeptDesc = seed.Level5DeptDesc,
            LoadDate = loadedAt,
            LastUpdated = loadedAt,
        };
    }

    private static Person CreatePerson(PersonSeed seed, IReadOnlyDictionary<string, AppUser> usersByIamId)
    {
        var names = seed.DisplayName.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var firstName = names.Length > 0 ? names[0] : seed.DisplayName;
        var lastName = names.Length > 1 ? names[1] : seed.LastNameFallback;

        return new Person
        {
            IamId = seed.IamId,
            EmployeeId = seed.EmployeeId,
            StudentId = null,
            ExternalId = null,
            FirstName = firstName,
            MiddleName = null,
            LastName = lastName,
            Suffix = null,
            FullName = seed.DisplayName,
            Pronouns = null,
            IsEmployee = seed.IsEmployee,
            IsHsEmployee = false,
            IsFaculty = seed.IsFaculty,
            IsStudent = false,
            IsStaff = seed.IsStaff,
            IsExternal = false,
            PrivacyCode = "N",
            IsCampusEmployee = seed.IsEmployee ? "Y" : "N",
            UserId = usersByIamId[seed.IamId].EmployeeId,
            Email = seed.Email,
            ModifyDate = ParseUtc(seed.ModifyDateUtc),
            ModifyDateRaw = ParseUtc(seed.ModifyDateUtc).ToString("yyyy-MM-dd HH:mm:ss"),
            FirstIngestedAt = ParseUtc(seed.ModifyDateUtc).AddDays(-14),
            LastFetchedAt = ParseUtc(seed.ModifyDateUtc),
            LastRunId = "11111111-2222-3333-4444-555555555555",
            SourceEndpoint = $"fabric://people/{seed.SourceDepartmentCode.ToLowerInvariant()}",
            PromotedAt = ParseUtc(seed.ModifyDateUtc).AddMinutes(15),
            PromotionRunId = "66666666-7777-8888-9999-000000000000",
        };
    }

    private static IEnumerable<LeaveRequestDay> ExpandLeaveRequestDays(
        LeaveRequestSeed seed,
        IReadOnlyDictionary<string, LeaveRequest> leaveRequestsByKey)
    {
        var startDate = ParseDateOnly(seed.StartDate);
        var endDate = ParseDateOnly(seed.EndDate);
        var requestKey = CreateLeaveRequestKey(seed.IamId, startDate, endDate);
        var leaveRequestId = leaveRequestsByKey[requestKey].Id;
        var totalDays = endDate.DayNumber - startDate.DayNumber + 1;
        var hoursPerDay = decimal.Round(seed.TotalHours / totalDays, 2);
        var allocatedHours = 0m;

        for (var offset = 0; offset < totalDays; offset++)
        {
            var leaveDate = startDate.AddDays(offset);
            var hours = offset == totalDays - 1
                ? seed.TotalHours - allocatedHours
                : hoursPerDay;

            allocatedHours += hours;

            yield return new LeaveRequestDay
            {
                LeaveRequestId = leaveRequestId,
                LeaveDate = leaveDate,
                Hours = hours,
            };
        }
    }

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

    private sealed record PersonSeed(
        string IamId,
        string EmployeeId,
        string DisplayName,
        string? LastNameFallback,
        string? Email,
        bool IsEmployee,
        bool IsFaculty,
        bool IsStaff,
        bool IsCampusEmployee,
        string SourceDepartmentCode,
        string ModifyDateUtc);

    private sealed record EmployeeReportingDepartmentOverrideSeed(
        string IamId,
        string DepartmentCode,
        string EffectiveStartDate,
        string? EffectiveEndDateExclusive,
        string? Reason,
        string CreatedByIamId,
        string CreatedUtc,
        string? ClosedByIamId,
        string? ClosedUtc);

    private sealed record DepartmentChairAssignmentSeed(
        string DepartmentCode,
        string IamId,
        string EffectiveStartDate,
        string? EffectiveEndDateExclusive,
        string CreatedByIamId,
        string CreatedUtc,
        string? ClosedByIamId,
        string? ClosedUtc);

    private sealed record ClusterCaoAssignmentSeed(
        string ClusterName,
        string IamId,
        string EffectiveStartDate,
        string? EffectiveEndDateExclusive,
        string CreatedByIamId,
        string CreatedUtc,
        string? ClosedByIamId,
        string? ClosedUtc);

    private sealed record LeaveTypeSeed(
        string LeaveTypeKey,
        int? SourceLeaveTypeNumber,
        string DisplayName,
        bool HasAccrualBalance,
        bool IsActive);

    private sealed record EmployeeAccrualBalanceSeed(
        string EmployeeId,
        string EmployeeEmail,
        string EmployeeName,
        string AsOfDate,
        string PositionNumber,
        int LeaveTypeNumber,
        string TypeLabel,
        decimal PrevBal,
        decimal HoursTaken,
        decimal AccrualHours,
        decimal AdjustedHours,
        decimal CalculatedBal,
        decimal AccrualLimit,
        string EmployeeClassCode,
        string EmployeeClassDescription,
        string JobCode,
        string JobCodeDescription,
        string Level5Dept,
        string Level5DeptDesc);

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

    private sealed record OutboundMessageSeed(
        string RequestIamId,
        string RequestStartDate,
        string RequestEndDate,
        string NotificationType,
        string RecipientEmail,
        OutboundMessageStatus Status,
        string NotBeforeUtc,
        string? SentUtc,
        int AttemptCount,
        string? LastError,
        string? ProviderMessageId);
}
