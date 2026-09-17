using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Core.Notification;

namespace Server.Tests.Notification;

public sealed class LeaveRequestEmailDeliveryServiceTests
{
    [Fact]
    public async Task ProcessDueAsync_renews_a_slow_delivery_while_another_worker_processes_unclaimed_and_expired_messages()
    {
        const int BatchSize = 50;
        var databasePath = Path.Combine(Path.GetTempPath(), $"leaves-email-delivery-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        try
        {
            var nowUtc = DateTime.UtcNow;
            await SeedAsync(options, nowUtc);
            var leaseRenewal = new LeaseRenewalObserver("first@example.test");

            using var services = new ServiceCollection()
                .AddScoped<AppDbContext>(_ => new SqliteAppDbContext(options, leaseRenewal))
                .BuildServiceProvider();
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            var slowDelivery = new ControlledNotificationService("first@example.test");
            var otherDelivery = new ControlledNotificationService();

            await using var firstDb = new SqliteAppDbContext(options, leaseRenewal);
            await using var secondDb = new SqliteAppDbContext(options, leaseRenewal);
            var firstWorker = CreateService(firstDb, slowDelivery, scopeFactory, BatchSize);
            var secondWorker = CreateService(secondDb, otherDelivery, scopeFactory, BatchSize);

            var firstWorkerTask = firstWorker.ProcessDueAsync(nowUtc, CancellationToken.None);
            await slowDelivery.SendStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await leaseRenewal.FirstMessageRenewed.WaitAsync(TimeSpan.FromSeconds(10));

            var secondWorkerResult = await secondWorker.ProcessDueAsync(nowUtc, CancellationToken.None);
            slowDelivery.ReleaseSend();
            var firstWorkerResult = await firstWorkerTask;

            firstWorkerResult.ClaimedCount.Should().Be(1);
            firstWorkerResult.SentCount.Should().Be(1);
            secondWorkerResult.ClaimedCount.Should().Be(BatchSize);
            secondWorkerResult.SentCount.Should().Be(BatchSize);
            slowDelivery.DeliveredTo.Should().Equal("first@example.test");
            otherDelivery.DeliveredTo.Should().Equal(
                Enumerable.Range(2, BatchSize - 1).Select(index => $"queued-{index:D2}@example.test")
                    .Append("expired@example.test"));

            await using var verificationDb = new SqliteAppDbContext(options, leaseRenewal);
            var messages = await verificationDb.OutboundMessages.OrderBy(message => message.Id).ToListAsync();
            messages.Should().OnlyContain(message => message.Status == OutboundMessageStatus.Sent);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static LeaveRequestEmailDeliveryService CreateService(
        AppDbContext db,
        INotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        int batchSize)
    {
        return new LeaveRequestEmailDeliveryService(
            db,
            notificationService,
            Options.Create(new EmailDeliveryOptions
            {
                Enabled = true,
                BatchSize = batchSize,
                LockDurationMinutes = 15,
                LeaseRenewalIntervalSeconds = 1,
            }),
            scopeFactory,
            NullLogger<LeaveRequestEmailDeliveryService>.Instance);
    }

    private static async Task SeedAsync(DbContextOptions<AppDbContext> options, DateTime nowUtc)
    {
        await using var db = new SqliteAppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var requester = new AppUser
        {
            EntraObjectId = Guid.NewGuid(),
            IamId = "requester1",
            Email = "requester@example.test",
            DisplayName = "Requesting Faculty",
            FirstLoginUtc = nowUtc,
        };
        var leaveType = new LeaveType
        {
            LeaveTypeKey = "vacation",
            DisplayName = "Vacation",
        };
        db.AddRange(requester, leaveType);
        await db.SaveChangesAsync();

        var request = new LeaveRequest
        {
            AppUserId = requester.Id,
            IamId = requester.IamId,
            LeaveTypeId = leaveType.Id,
            Status = LeaveRequestStatus.Approved,
            StartDate = new DateOnly(2026, 9, 14),
            EndDate = new DateOnly(2026, 9, 14),
            TotalHours = 8,
            ReportingDepartmentCodeSnapshot = "123456",
            ReportingDepartmentNameSnapshot = "Example Department",
            WorkflowModeSnapshot = WorkflowMode.DirectSubmission,
            SubmittedAt = nowUtc,
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();

        db.OutboundMessages.Add(CreateMessage(request.Id, "first@example.test", nowUtc));
        db.OutboundMessages.AddRange(
            Enumerable.Range(2, 49)
                .Select(index => CreateMessage(request.Id, $"queued-{index:D2}@example.test", nowUtc)));
        db.OutboundMessages.Add(CreateMessage(request.Id, "expired@example.test", nowUtc, nowUtc.AddMinutes(-1)));
        await db.SaveChangesAsync();
    }

    private static OutboundMessage CreateMessage(int requestId, string recipientEmail, DateTime nowUtc, DateTime? lockedUntilUtc = null) => new()
    {
        LeaveRequestId = requestId,
        NotificationType = LeaveRequestNotificationTypes.Approved,
        RecipientEmail = recipientEmail,
        DedupeKey = $"leave-request:{requestId}:{recipientEmail}",
        Status = OutboundMessageStatus.Pending,
        NotBeforeUtc = nowUtc,
        CreatedUtc = nowUtc,
        LockId = lockedUntilUtc is null ? null : Guid.NewGuid(),
        LockedUntilUtc = lockedUntilUtc,
    };

    private sealed class ControlledNotificationService : INotificationService
    {
        private readonly string? _blockedRecipient;
        private readonly TaskCompletionSource _sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ControlledNotificationService(string? blockedRecipient = null)
        {
            _blockedRecipient = blockedRecipient;
        }

        public List<string> DeliveredTo { get; } = [];

        public Task SendStarted => _sendStarted.Task;

        public void ReleaseSend() => _release.TrySetResult();

        public async Task SendAsync(
            EmailRecipients recipients,
            string subject,
            string header,
            string message,
            CancellationToken cancellationToken = default)
        {
            var recipient = recipients.To.Single();
            DeliveredTo.Add(recipient);

            if (!string.Equals(recipient, _blockedRecipient, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _sendStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }

        public Task SendTableAsync(
            EmailRecipients recipients,
            string subject,
            string header,
            string message,
            IReadOnlyList<NotificationTableRow> rows,
            decimal totalAmount,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class LeaseRenewalObserver
    {
        private readonly string _recipientEmail;
        private readonly TaskCompletionSource _firstMessageRenewed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _lockUpdates;

        public LeaseRenewalObserver(string recipientEmail)
        {
            _recipientEmail = recipientEmail;
        }

        public Task FirstMessageRenewed => _firstMessageRenewed.Task;

        public void Observe(ChangeTracker changeTracker)
        {
            var renewed = changeTracker.Entries<OutboundMessage>().Any(entry =>
                entry.State == EntityState.Modified &&
                string.Equals(entry.Entity.RecipientEmail, _recipientEmail, StringComparison.OrdinalIgnoreCase) &&
                entry.Property(message => message.LockedUntilUtc).IsModified);
            if (renewed && Interlocked.Increment(ref _lockUpdates) >= 3)
            {
                _firstMessageRenewed.TrySetResult();
            }
        }
    }

    private sealed class SqliteAppDbContext(
        DbContextOptions<AppDbContext> options,
        LeaseRenewalObserver? leaseRenewal = null) : AppDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            leaseRenewal?.Observe(ChangeTracker);
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<AppAdminAssignment>();
            modelBuilder.Ignore<Cluster>();
            modelBuilder.Ignore<ClusterCaoAssignment>();
            modelBuilder.Ignore<CurrentAccrualBalance>();
            modelBuilder.Ignore<CurrentEmployee>();
            modelBuilder.Ignore<Department>();
            modelBuilder.Ignore<DepartmentChairAssignment>();
            modelBuilder.Ignore<DepartmentEmailRouting>();
            modelBuilder.Ignore<EmployeeReportingDepartmentOverride>();
            modelBuilder.Ignore<EmployeeAccrualBalance>();
            modelBuilder.Ignore<Person>();
            modelBuilder.Ignore<LeaveRequestAction>();
            modelBuilder.Ignore<LeaveRequestDay>();

            var appUser = modelBuilder.Entity<AppUser>();
            AppUser.Configure(appUser);
            appUser.Ignore(user => user.CreatedAdminAssignments);
            appUser.Ignore(user => user.CreatedClusters);
            appUser.Ignore(user => user.CreatedEmployeeReportingDepartmentOverrides);
            appUser.Ignore(user => user.ClosedEmployeeReportingDepartmentOverrides);
            appUser.Ignore(user => user.CreatedDepartmentChairAssignments);
            appUser.Ignore(user => user.ClosedDepartmentChairAssignments);
            appUser.Ignore(user => user.CreatedClusterCaoAssignments);
            appUser.Ignore(user => user.ClosedClusterCaoAssignments);
            appUser.Ignore(user => user.UpdatedDepartmentEmailRoutings);
            appUser.Ignore(user => user.LeaveRequestActions);
            LeaveType.Configure(modelBuilder.Entity<LeaveType>());
            var leaveRequest = modelBuilder.Entity<LeaveRequest>();
            LeaveRequest.Configure(leaveRequest);
            leaveRequest.Ignore(request => request.Actions);
            leaveRequest.Ignore(request => request.Days);
            OutboundMessage.Configure(modelBuilder.Entity<OutboundMessage>());
        }
    }
}
