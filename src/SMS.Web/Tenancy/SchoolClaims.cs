namespace SMS.Web.Tenancy;

/// <summary>
/// The claims that carry school and branch context in the authentication cookie.
/// <para>
/// These are the <i>session</i> context, not the authorization source of truth. Every one
/// of them is re-validated against <c>master</c> on each request by
/// <see cref="TenantResolutionMiddleware"/>, so a stale cookie cannot outlive a revoked
/// membership — architecture sections 5.4 and 5.6.
/// </para>
/// </summary>
public static class SchoolClaims
{
    public const string SchoolId = "sms:school_id";
    public const string BranchScope = "sms:branch_scope";
    public const string ActiveBranchId = "sms:active_branch_id";
    public const string MembershipVersion = "sms:membership_version";
    public const string SchoolRole = "sms:school_role";
}
