using Microsoft.Extensions.Options;

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

    // A delivery can take longer than a single SMTP socket operation. Renew its
    // database lease while rendering and sending so another worker cannot reclaim it.
    public int LeaseRenewalIntervalSeconds { get; init; } = 30;
}

public sealed class EmailDeliveryOptionsValidator : IValidateOptions<EmailDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailDeliveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (options.BatchSize <= 0)
        {
            failures.Add("EmailDelivery:BatchSize must be greater than zero when email delivery is enabled.");
        }

        if (options.LockDurationMinutes <= 0)
        {
            failures.Add("EmailDelivery:LockDurationMinutes must be greater than zero when email delivery is enabled.");
        }

        if (options.LeaseRenewalIntervalSeconds <= 0)
        {
            failures.Add("EmailDelivery:LeaseRenewalIntervalSeconds must be greater than zero when email delivery is enabled.");
        }
        else if (options.LockDurationMinutes > 0 &&
                 options.LeaseRenewalIntervalSeconds >= TimeSpan.FromMinutes(options.LockDurationMinutes).TotalSeconds)
        {
            failures.Add("EmailDelivery:LeaseRenewalIntervalSeconds must be shorter than EmailDelivery:LockDurationMinutes when email delivery is enabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
