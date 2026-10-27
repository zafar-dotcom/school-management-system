namespace SMS.Api.Contracts;

public sealed record TokenRequest(string Email, string Password);

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string TokenType = "Bearer");

public sealed record ClassResponse(Guid Id, string Code, string Name, int SortOrder);

public sealed record ClassListResponse(
    string SchoolName,
    string BranchName,
    Guid ActiveBranchId,
    IReadOnlyList<ClassResponse> Classes);

/// <summary>
/// Note the absence of a branch. <c>BranchId</c> is set server-side from the resolved
/// token context — a client cannot name a branch, by design (architecture section 4.6).
/// </summary>
public sealed record CreateClassRequest(string Code, string Name, int SortOrder);
