namespace WmsDistributionCenter.Api.Security;

/// <summary>Names of the built-in roles seeded at startup.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Supervisor, Operator, Viewer];
}

/// <summary>Authorization policy names. See the permission matrix in the README.</summary>
public static class Policies
{
    /// <summary>Any authenticated user (read access).</summary>
    public const string Read = "Read";
    /// <summary>Floor work: Admin, Supervisor, Operator.</summary>
    public const string Operate = "Operate";
    /// <summary>Planning and master data: Admin, Supervisor.</summary>
    public const string Manage = "Manage";
    /// <summary>Administration and destructive operations: Admin.</summary>
    public const string AdminOnly = "AdminOnly";
}
