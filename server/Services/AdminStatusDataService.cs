using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminStatusDataService
{
    private readonly AppDbContext _db;

    public AdminStatusDataService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminStatusData> LoadStatusDataAsync(CancellationToken cancellationToken)
    {
        var pendingRequestCount = await _db.LeaveRequests
            .AsNoTracking()
            .CountAsync(request => request.Status == LeaveRequestStatus.PendingApproval, cancellationToken);
        var latestPeoplePromotionAt = await _db.People
            .Select(person => person.PromotedAt)
            .MaxAsync(cancellationToken);
        var latestAccrualUpdatedAt = await _db.EmployeeAccrualBalances
            .Select(row => (DateTime?)row.LastUpdated)
            .MaxAsync(cancellationToken);
        var currentAccrualBalances = await _db.CurrentAccrualBalances
            .ToListAsync(cancellationToken);

        return new AdminStatusData(
            CurrentAccrualBalances: currentAccrualBalances,
            LatestAccrualUpdatedAt: latestAccrualUpdatedAt,
            LatestPeoplePromotionAt: latestPeoplePromotionAt,
            PendingRequestCount: pendingRequestCount);
    }
}

public sealed record AdminStatusData(
    IReadOnlyList<CurrentAccrualBalance> CurrentAccrualBalances,
    DateTime? LatestAccrualUpdatedAt,
    DateTime? LatestPeoplePromotionAt,
    int PendingRequestCount);
