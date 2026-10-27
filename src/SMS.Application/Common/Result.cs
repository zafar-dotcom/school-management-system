namespace SMS.Application.Common;

/// <summary>
/// The outcome of a use case, without exceptions for the ordinary failures.
/// <para>
/// Handlers are called by more than one host. An MVC controller turns these errors into
/// <c>ModelState</c>, the API turns them into a problem response — neither should have to
/// catch an exception to find out that a password was wrong.
/// </para>
/// </summary>
public class Result
{
    protected Result(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public bool Succeeded { get; }
    public IReadOnlyList<string> Errors { get; }

    public static Result Success() => new(true, []);
    public static Result Failure(params string[] errors) => new(false, errors);
    public static Result<T> Success<T>(T value) => Result<T>.Ok(value);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool succeeded, T? value, IReadOnlyList<string> errors)
        : base(succeeded, errors) => _value = value;

    /// <summary>Only valid when <see cref="Result.Succeeded"/>; throws otherwise.</summary>
    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException("A failed result has no value. Check Succeeded first.");

    internal static Result<T> Ok(T value) => new(true, value, []);

    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);
}
