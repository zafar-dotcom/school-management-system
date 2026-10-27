namespace SMS.Application.Access;

/// <summary>
/// The claim types carrying school and branch context.
/// <para>
/// Shared by both hosts on purpose: the web cookie and the API token must name these
/// identically, or tenant resolution would have to be written twice and could drift.
/// </para>
/// <para>
/// These are session context, not the authorization source of truth. Every one is
/// re-validated against <c>master</c> on each request — architecture sections 5.4 and 5.6.
/// </para>
/// </summary>
public static class SchoolClaimTypes
{
    public const string SchoolId = "sms:school_id";
    public const string BranchScope = "sms:branch_scope";
    public const string ActiveBranchId = "sms:active_branch_id";
    public const string MembershipVersion = "sms:membership_version";
    public const string SchoolRole = "sms:school_role";
}
