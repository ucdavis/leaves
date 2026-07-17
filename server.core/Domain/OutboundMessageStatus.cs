namespace Server.Core.Domain;

public enum OutboundMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    DeadLetter = 3,
}
