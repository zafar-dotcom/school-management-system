namespace SMS.Domain.Common;

public enum SchoolStatus { Provisioning, Active, Suspended, Closed }

public enum BranchStatus { Active, Suspended, Closed }

public enum UserStatus { Active, Suspended, Disabled }

public enum MembershipStatus { Active, Suspended }

/// <summary>Whether a membership covers every branch of the school, or a named set.</summary>
public enum BranchScope { AllBranches, Specific }

/// <summary>
/// Roles are held on <see cref="Master.SchoolMembership"/>, never in ASP.NET Identity's
/// role tables — those cannot express "teacher at school A, accountant at school B".
/// </summary>
public enum SchoolRole { SchoolAdmin, BranchAdmin, Teacher, Accountant, Student, Parent }

public enum LicenseStatus { Valid, Expiring, Grace, ReadOnly, Invalid, Superseded }

public enum AuditAction { Insert, Update, Delete }
