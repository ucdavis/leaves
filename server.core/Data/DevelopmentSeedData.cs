namespace Server.Core.Data;

public static class DevelopmentSeedData
{
    public static readonly Guid LocalAdminEntraObjectId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    public static readonly Guid LocalRequesterEntraObjectId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    public static readonly Guid LocalUnauthorizedEntraObjectId = Guid.Parse("14141414-1414-1414-1414-141414141414");

    public const string LocalAdminDisplayName = "Local Admin";
    public const string LocalAdminEmail = "admin@local.test";
    public const string LocalAdminIamId = "devadmin";
    public const string LocalAdminEmployeeId = "10000001";

    public const string LocalRequesterDisplayName = "Local Requester";
    public const string LocalRequesterEmail = "requester@local.test";
    public const string LocalRequesterIamId = "devreq";
    public const string LocalRequesterEmployeeId = "10000002";

    public const string LocalUnauthorizedDisplayName = "Local Unauthorized";
    public const string LocalUnauthorizedEmail = "unauthorized@local.test";
    public const string LocalUnauthorizedIamId = "devunauth";
    public const string LocalUnauthorizedEmployeeId = "10000003";
}
