using FluentAssertions;
using Server.Core.Domain;
using Server.Services;

namespace Server.Tests.Services;

public sealed class AdminStatusDataServiceTests
{
    [Fact]
    public async Task LoadStatusDataAsync_counts_only_emails_that_stopped_retrying()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        foreach (var status in new[]
                 {
                     OutboundMessageStatus.Pending,
                     OutboundMessageStatus.Sent,
                     OutboundMessageStatus.Failed,
                     OutboundMessageStatus.Failed,
                     OutboundMessageStatus.DeadLetter,
                 })
        {
            db.OutboundMessages.Add(new OutboundMessage
            {
                LeaveRequestId = 1,
                NotificationType = "LeaveRequestApproved",
                RecipientEmail = "recipient@example.test",
                Status = status,
                DedupeKey = Guid.NewGuid().ToString(),
            });
        }

        await db.SaveChangesAsync();

        var result = await new AdminStatusDataService(db).LoadStatusDataAsync(CancellationToken.None);

        result.UndeliveredEmailCount.Should().Be(1);
    }
}
