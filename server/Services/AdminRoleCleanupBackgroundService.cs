using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminRoleCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminRoleCleanupBackgroundService> _logger;

    public AdminRoleCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AdminRoleCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task CleanupInactiveRoleAssignmentsAsync(
        int? closedByAppUserId,
        CancellationToken cancellationToken)
    {
        return RunCleanupAsync(closedByAppUserId, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(null, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(int? closedByAppUserId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var directoryDataService = scope.ServiceProvider.GetRequiredService<AdminDirectoryDataService>();

            var roleOptionsData = await directoryDataService.LoadRoleOptionsDataAsync(cancellationToken);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var activeCaoAssignments = await db.ClusterCaoAssignments
                .Where(assignment => assignment.ClosedUtc == null &&
                                     assignment.EffectiveStartDate <= today &&
                                     (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today))
                .ToListAsync(cancellationToken);
            var activeChairAssignments = await db.DepartmentChairAssignments
                .Where(assignment => assignment.ClosedUtc == null &&
                                     assignment.EffectiveStartDate <= today &&
                                     (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today))
                .ToListAsync(cancellationToken);
            var activeAdminAssignments = await db.AppAdminAssignments
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var changes = AdminRolesService.GetInactiveRoleAssignmentChanges(
                activeAdminAssignments,
                activeCaoAssignments,
                activeChairAssignments,
                roleOptionsData.CurrentEmployees,
                roleOptionsData.Clusters,
                roleOptionsData.Departments);

            if (changes.AdminAssignmentsToDelete.Count == 0 &&
                changes.CaoAssignmentsToClose.Count == 0 &&
                changes.ChairAssignmentsToClose.Count == 0)
            {
                return;
            }

            foreach (var assignment in changes.AdminAssignmentsToDelete)
            {
                db.AppAdminAssignments.Remove(assignment);
            }

            foreach (var assignment in changes.CaoAssignmentsToClose)
            {
                AdminRolesService.CloseClusterCaoAssignment(assignment, closedByAppUserId, now, today);
            }

            foreach (var assignment in changes.ChairAssignmentsToClose)
            {
                AdminRolesService.CloseDepartmentChairAssignment(assignment, closedByAppUserId, now, today);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogInformation(ex, "Skipped inactive role cleanup because another request updated the same rows.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin role cleanup job failed.");
        }
    }
}
