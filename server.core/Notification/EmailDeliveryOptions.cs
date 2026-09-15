namespace Server.Core.Notification;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    // Delivery is opt-in so deploying the queue cannot accidentally send mail.
    public bool Enabled { get; init; }

    // New messages wake the worker immediately. This interval only recovers work
    // left behind by a process restart or a failed delivery attempt.
    public int RecoveryIntervalSeconds { get; init; } = 60;

    public int BatchSize { get; init; } = 50;

    public int MaxAttempts { get; init; } = 5;

    public int LockDurationMinutes { get; init; } = 15;
}
