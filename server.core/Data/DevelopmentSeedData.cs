namespace Server.Core.Data;

public static class DevelopmentSeedData
{
    public static readonly Guid LocalAdminEntraObjectId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    public static readonly Guid LocalFacultyEntraObjectId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    public static readonly Guid LocalChairEntraObjectId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    public static readonly Guid LocalCaoEntraObjectId = Guid.Parse("16161616-1616-1616-1616-161616161616");
    public static readonly Guid LocalUnauthorizedEntraObjectId = Guid.Parse("14141414-1414-1414-1414-141414141414");

    public const string TestClusterName = "Test Cluster";
    public const string TestDepartmentCode = "039999";
    public const string TestDepartmentName = "TEST DEPARTMENT";

    public const string LocalAdminDisplayName = "Local Admin";
    public const string LocalAdminEmail = "admin@local.test";
    public const string LocalAdminIamId = "devadmin";
    public const string LocalAdminEmployeeId = "10000001";

    public const string LocalFacultyDisplayName = "Test Faculty";
    public const string LocalFacultyEmail = "faculty@local.test";
    public const string LocalFacultyIamId = "devfaculty";
    public const string LocalFacultyEmployeeId = "10000002";

    public const string LocalChairDisplayName = "Test Chair";
    public const string LocalChairEmail = "chair@local.test";
    public const string LocalChairIamId = "devchair";
    public const string LocalChairEmployeeId = "10000004";

    public const string LocalCaoDisplayName = "Test CAO";
    public const string LocalCaoEmail = "cao@local.test";
    public const string LocalCaoIamId = "devcao";
    public const string LocalCaoEmployeeId = "10000005";

    public const string LocalUnauthorizedDisplayName = "Local Unauthorized";
    public const string LocalUnauthorizedEmail = "unauthorized@local.test";
    public const string LocalUnauthorizedIamId = "devunauth";
    public const string LocalUnauthorizedEmployeeId = "10000003";
}
