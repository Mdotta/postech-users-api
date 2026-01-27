namespace Postech.Users.Api.Domain.Results;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string? error)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException("Cannot be successful and contain an error message.");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("Failure must have an error message.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new Result(true, null);

    public static Result Failure(string error) => new Result(false, error);

    public static Result<T> Success<T>(T value) => new Result<T>(value, true, null);

    public static Result<T> Failure<T>(string error) => new Result<T>(default!, false, error);

}

public class Result<T> : Result
{
    public T Value { get; }

    protected internal Result(T value, bool isSuccess, string? error)
        : base(isSuccess, error)
    {
        Value = value;
    }
}