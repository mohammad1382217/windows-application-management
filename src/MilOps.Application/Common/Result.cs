namespace MilOps.Application.Common;

/// <summary>
/// Discriminated result for operations that can fail with domain/validation errors.
/// Avoids throwing across the Application/Presentation boundary so the UI can
/// render errors uniformly.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string Error { get; }
    public string Code { get; }

    protected Result(bool isSuccess, string error, string code)
    {
        IsSuccess = isSuccess;
        Error = error;
        Code = code;
    }

    public static Result Success() => new(true, string.Empty, string.Empty);
    public static Result Failure(string code, string error) => new(false, error, code);

    public static Result<T> Success<T>(T value) => new(value, true, string.Empty, string.Empty);
    public static Result<T> Failure<T>(string code, string error) =>
        new(default, false, error, code);
}

public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, string error, string code)
        : base(isSuccess, error, code) => Value = value;

    /// <summary>Implicitly unwrap a successful result's value for ergonomic call sites.</summary>
    public static implicit operator Result<T>(T value) => Success(value);
}
