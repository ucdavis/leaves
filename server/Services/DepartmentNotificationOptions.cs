namespace Server.Services;

public sealed class DepartmentNotificationOptions
{
    public const string SectionName = "DepartmentNotifications";

    public string DefaultRoutingEmail { get; init; } = string.Empty;
}
