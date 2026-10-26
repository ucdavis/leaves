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
    public async Task ProcessDueAsync_keeps_sent_recipients_sent_when_another_recipient_for_the_same_request_fails()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"leaves-email-delivery-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        try
        {
            var nowUtc = DateTime.UtcNow;
            await SeedAsync(options, nowUtc, messageCount: 1);
            await using (var setupDb = new SqliteAppDbContext(options))
            {
                var requestId = await setupDb.LeaveRequests.Select(request => request.Id).SingleAsync();
                setupDb.OutboundMessages.AddRange(
                    CreateMessage(requestId, "second@example.test", nowUtc),
                    CreateMessage(requestId, "failing@example.test", nowUtc));
                await setupDb.SaveChangesAsync();
            }

            using var services = new ServiceCollection()
                .AddScoped<AppDbContext>(_ => new SqliteAppDbContext(options))
                .BuildServiceProvider();
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            var delivery = new SelectivelyFailingNotificationService("failing@example.test");
            await using var db = new SqliteAppDbContext(options);
            var service = CreateService(db, delivery, scopeFactory, batchSize: 3);

            var result = await service.ProcessDueAsync(nowUtc, CancellationToken.None);

            result.Should().Be(new LeaveRequestEmailDeliveryResult(3, 2, 1, 0));
            delivery.DeliveredTo.Should().Equal("first@example.test", "second@example.test");
            await using var verificationDb = new SqliteAppDbContext(options);
            var messages = await verificationDb.OutboundMessages.ToListAsync();
            messages.Should().Contain(message =>
                message.RecipientEmail == "first@example.test" &&
                message.Status == OutboundMessageStatus.Sent);
            messages.Should().Contain(message =>
                message.RecipientEmail == "second@example.test" &&
                message.Status == OutboundMessageStatus.Sent);
            var failedMessage = messages.Should().ContainSingle(message =>
                message.RecipientEmail == "failing@example.test").Subject;
            failedMessage.Status.Should().Be(OutboundMessageStatus.Failed);
            failedMessage.AttemptCount.Should().Be(1);
            failedMessage.LastError.Should().Be("SMTP server is unavailable.");
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task QueueDecisionAsync_rolls_back_every_recipient_with_the_approval_transaction()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"leaves-email-delivery-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        try
        {
            await using (var db = new SqliteAppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                var requester = new AppUser
                {
                    EntraObjectId = Guid.NewGuid(),
                    IamId = "requester",
                    Email = "requester@example.test",
                    FirstLoginUtc = DateTime.UtcNow,
                };
                db.Add(requester);
                db.Departments.Add(new Department
                {
                    DepartmentCode = "123456",
                    DepartmentName = "Example Department",
                });
                db.LeaveTypes.Add(new LeaveType
                {
                    LeaveTypeKey = "vacation",
                    DisplayName = "Vacation",
                });
                await db.SaveChangesAsync();
                db.DepartmentEmailRoutings.Add(new DepartmentEmailRouting
                {
                    DepartmentCode = "123456",
                    ToEmail = "routing@example.test",
                    UpdatedByAppUserId = requester.Id,
                });
                await db.SaveChangesAsync();

                await using var transaction = await db.Database.BeginTransactionAsync();
                var request = new LeaveRequest
                {
                    AppUserId = requester.Id,
                    IamId = requester.IamId,
                    LeaveTypeId = 1,
                    Status = LeaveRequestStatus.Approved,
                    StartDate = new DateOnly(2026, 9, 14),
                    EndDate = new DateOnly(2026, 9, 14),
                    TotalHours = 8,
                    ReportingDepartmentCodeSnapshot = "123456",
                    ReportingDepartmentNameSnapshot = "Example Department",
                    WorkflowModeSnapshot = WorkflowMode.ApprovalRequired,
                    SubmittedAt = DateTime.UtcNow,
                };
                db.LeaveRequests.Add(request);
                await db.SaveChangesAsync();
                await CreateQueue(db).QueueDecisionAsync(request, requester, CancellationToken.None);
                await db.SaveChangesAsync();

                await transaction.RollbackAsync();
            }

            await using var verificationDb = new SqliteAppDbContext(options);
            (await verificationDb.LeaveRequests.CountAsync()).Should().Be(0);
            (await verificationDb.OutboundMessages.CountAsync()).Should().Be(0);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProcessDueAsync_records_exponential_retries_then_dead_letters_after_max_attempts()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"leaves-email-delivery-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        try
        {
            var nowUtc = DateTime.UtcNow;
            await SeedAsync(options, nowUtc, messageCount: 1);

            using var services = new ServiceCollection()
                .AddScoped<AppDbContext>(_ => new SqliteAppDbContext(options))
                .BuildServiceProvider();
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            var failingDelivery = new FailingNotificationService("SMTP server is unavailable.");

            await using var firstDb = new SqliteAppDbContext(options);
            var firstAttempt = CreateService(
                firstDb,
                failingDelivery,
                scopeFactory,
                batchSize: 1,
                maxAttempts: 3);

            var firstResult = await firstAttempt.ProcessDueAsync(nowUtc, CancellationToken.None);

            firstResult.Should().Be(new LeaveRequestEmailDeliveryResult(1, 0, 1, 0));
            await using var afterFirstAttempt = new SqliteAppDbContext(options);
            var failedMessage = await afterFirstAttempt.OutboundMessages.SingleAsync();
            failedMessage.Status.Should().Be(OutboundMessageStatus.Failed);
            failedMessage.AttemptCount.Should().Be(1);
            failedMessage.LastError.Should().Be("SMTP server is unavailable.");
            failedMessage.NotBeforeUtc.Should().Be(nowUtc.AddMinutes(2));
            failedMessage.LockId.Should().BeNull();
            failedMessage.LockedUntilUtc.Should().BeNull();

            await using var secondDb = new SqliteAppDbContext(options);
            var secondAttempt = CreateService(
                secondDb,
                failingDelivery,
                scopeFactory,
                batchSize: 1,
                maxAttempts: 3);

            var secondResult = await secondAttempt.ProcessDueAsync(failedMessage.NotBeforeUtc, CancellationToken.None);

            secondResult.Should().Be(new LeaveRequestEmailDeliveryResult(1, 0, 1, 0));
            await using var afterSecondAttempt = new SqliteAppDbContext(options);
            var retriedMessage = await afterSecondAttempt.OutboundMessages.SingleAsync();
            retriedMessage.Status.Should().Be(OutboundMessageStatus.Failed);
            retriedMessage.AttemptCount.Should().Be(2);
            retriedMessage.NotBeforeUtc.Should().Be(nowUtc.AddMinutes(6));

            await using var thirdDb = new SqliteAppDbContext(options);
            var thirdAttempt = CreateService(
                thirdDb,
                failingDelivery,
                scopeFactory,
                batchSize: 1,
                maxAttempts: 3);

            var thirdResult = await thirdAttempt.ProcessDueAsync(retriedMessage.NotBeforeUtc, CancellationToken.None);

            thirdResult.Should().Be(new LeaveRequestEmailDeliveryResult(1, 0, 0, 1));
            await using var afterThirdAttempt = new SqliteAppDbContext(options);
            var deadLetter = await afterThirdAttempt.OutboundMessages.SingleAsync();
            deadLetter.Status.Should().Be(OutboundMessageStatus.DeadLetter);
            deadLetter.AttemptCount.Should().Be(3);
            deadLetter.LastError.Should().Be("SMTP server is unavailable.");
            failingDelivery.AttemptCount.Should().Be(3);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

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
            slowDelivery.DeliveredSubjects.Should().Equal("Leave request approved");
            var routingMessage = slowDelivery.DeliveredMessages.Should().ContainSingle().Subject;
            routingMessage.Should().ContainAll(
                "Request ID:",
                "Name: Requesting Faculty",
                "Email: requester@example.test",
                "Department: Example Department",
                "Leave type: Vacation",
                "FMLA pay type: Sick Leave",
                "Date(s): September 14, 2026",
                "Total hours: 8",
                "Note: Coverage confirmed with my team.",
                "Approved by: Approving Chair",
                "Approval comment: Coverage confirmed.");
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

    [Fact]
    public async Task ProcessDueAsync_dead_letters_an_orphaned_message_without_sending()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"leaves-email-delivery-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        try
        {
            var nowUtc = DateTime.UtcNow;
            await SeedAsync(options, nowUtc, messageCount: 1);
            await using (var setupDb = new SqliteAppDbContext(options))
            {
                await setupDb.Database.OpenConnectionAsync();
                await setupDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
                await setupDb.Database.ExecuteSqlRawAsync("DELETE FROM LeaveRequest");
                await setupDb.Database.CloseConnectionAsync();
            }

            using var services = new ServiceCollection()
                .AddScoped<AppDbContext>(_ => new SqliteAppDbContext(options))
                .BuildServiceProvider();
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
            var delivery = new ControlledNotificationService();
            await using var db = new SqliteAppDbContext(options);
            var service = CreateService(db, delivery, scopeFactory, batchSize: 1);

            var result = await service.ProcessDueAsync(nowUtc, CancellationToken.None);

            result.Should().Be(new LeaveRequestEmailDeliveryResult(1, 0, 0, 1));
            delivery.DeliveredTo.Should().BeEmpty();
            await using var verificationDb = new SqliteAppDbContext(options);
            var message = await verificationDb.OutboundMessages.SingleAsync();
            message.Status.Should().Be(OutboundMessageStatus.DeadLetter);
            message.AttemptCount.Should().Be(1);
            message.LastError.Should().Be("The related leave request could not be loaded.");
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
        int batchSize,
        int maxAttempts = 5)
    {
        return new LeaveRequestEmailDeliveryService(
            db,
            notificationService,
            Options.Create(new EmailDeliveryOptions
            {
                Enabled = true,
                BatchSize = batchSize,
                MaxAttempts = maxAttempts,
                LockDurationMinutes = 15,
                LeaseRenewalIntervalSeconds = 1,
            }),
            scopeFactory,
            NullLogger<LeaveRequestEmailDeliveryService>.Instance);
    }

    private static LeaveRequestNotificationQueue CreateQueue(AppDbContext db) =>
        new(db, NullLogger<LeaveRequestNotificationQueue>.Instance);

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        DateTime nowUtc,
        int messageCount = 50)
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
        var approver = new AppUser
        {
            EntraObjectId = Guid.NewGuid(),
            IamId = "approver1",
            DisplayName = "Approving Chair",
            FirstLoginUtc = nowUtc,
        };
        var leaveType = new LeaveType
        {
            LeaveTypeKey = "vacation",
            DisplayName = "Vacation",
        };
        var payLeaveType = new LeaveType
        {
            LeaveTypeKey = "sick",
            DisplayName = "Sick Leave",
        };
        db.AddRange(requester, approver, leaveType, payLeaveType);
        await db.SaveChangesAsync();

        var request = new LeaveRequest
        {
            AppUserId = requester.Id,
            IamId = requester.IamId,
            LeaveTypeId = leaveType.Id,
            PayLeaveTypeId = payLeaveType.Id,
            Note = "Coverage confirmed with my team.",
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
        db.LeaveRequestActions.Add(new LeaveRequestAction
        {
            LeaveRequestId = request.Id,
            ActionType = LeaveRequestActionType.Approved,
            ActorAppUserId = approver.Id,
            ActorIamId = approver.IamId,
            ActionAt = nowUtc,
            Comment = "Coverage confirmed.",
        });
        await db.SaveChangesAsync();

        db.OutboundMessages.Add(CreateMessage(
            request.Id,
            "first@example.test",
            nowUtc,
            notificationType: LeaveRequestNotificationTypes.Approved));
        db.OutboundMessages.AddRange(
            Enumerable.Range(2, Math.Max(0, messageCount - 1))
                .Select(index => CreateMessage(request.Id, $"queued-{index:D2}@example.test", nowUtc)));
        if (messageCount > 1)
        {
            db.OutboundMessages.Add(CreateMessage(request.Id, "expired@example.test", nowUtc, nowUtc.AddMinutes(-1)));
        }
        await db.SaveChangesAsync();
    }

    private static OutboundMessage CreateMessage(
        int requestId,
        string recipientEmail,
        DateTime nowUtc,
        DateTime? lockedUntilUtc = null,
        string notificationType = LeaveRequestNotificationTypes.Approved) => new()
    {
        LeaveRequestId = requestId,
        NotificationType = notificationType,
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

        public List<string> DeliveredMessages { get; } = [];

        public List<string> DeliveredSubjects { get; } = [];

        public Task SendStarted => _sendStarted.Task;

        public void ReleaseSend() => _release.TrySetResult();

        public async Task SendLeaveRequestAsync(
            EmailRecipients recipients,
            string subject,
            LeaveRequestEmailContent content,
            CancellationToken cancellationToken = default)
        {
            var recipient = recipients.To.Single();
            DeliveredTo.Add(recipient);
            DeliveredMessages.Add(content.ToTextBody());
            DeliveredSubjects.Add(subject);

            if (!string.Equals(recipient, _blockedRecipient, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _sendStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class FailingNotificationService(string message) : INotificationService
    {
        public int AttemptCount { get; private set; }

        public Task SendLeaveRequestAsync(
            EmailRecipients recipients,
            string subject,
            LeaveRequestEmailContent content,
            CancellationToken cancellationToken = default)
        {
            AttemptCount++;
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SelectivelyFailingNotificationService(string failingRecipient) : INotificationService
    {
        public List<string> DeliveredTo { get; } = [];

        public Task SendLeaveRequestAsync(
            EmailRecipients recipients,
            string subject,
            LeaveRequestEmailContent content,
            CancellationToken cancellationToken = default)
        {
            var recipient = recipients.To.Single();
            if (string.Equals(recipient, failingRecipient, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SMTP server is unavailable.");
            }

            DeliveredTo.Add(recipient);
            return Task.CompletedTask;
        }
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
            modelBuilder.Ignore<CurrentFacultyAccrualBalance>();
            modelBuilder.Ignore<CurrentFacultyWithAccrual>();
            modelBuilder.Ignore<DepartmentChairAssignment>();
            modelBuilder.Ignore<EmployeeReportingDepartmentOverride>();
            modelBuilder.Ignore<EmployeeAccrualBalance>();
            modelBuilder.Ignore<Person>();
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
            var department = modelBuilder.Entity<Department>();
            department.HasKey(item => item.DepartmentCode);
            department.Ignore(item => item.Cluster);
            department.Ignore(item => item.DepartmentEmailRoutings);
            department.Ignore(item => item.EmployeeReportingDepartmentOverrides);
            department.Ignore(item => item.DepartmentChairAssignments);
            var routing = modelBuilder.Entity<DepartmentEmailRouting>();
            routing.HasKey(item => item.Id);
            routing.Property(item => item.Id).ValueGeneratedOnAdd();
            routing.Ignore(item => item.Department);
            LeaveType.Configure(modelBuilder.Entity<LeaveType>());
            var leaveRequest = modelBuilder.Entity<LeaveRequest>();
            LeaveRequest.Configure(leaveRequest);
            leaveRequest.Ignore(request => request.Days);
            LeaveRequestAction.Configure(modelBuilder.Entity<LeaveRequestAction>());
            OutboundMessage.Configure(modelBuilder.Entity<OutboundMessage>());
        }
    }
}
