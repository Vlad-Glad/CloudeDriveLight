namespace GDriveLight.Application.Common.Models;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && error != string.Empty)
            throw new InvalidOperationException("A successful result cannot contain an error.");
        if (!isSuccess && error == string.Empty)
            throw new InvalidOperationException("A failure result must contain an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected Result(T? value, bool isSuccess, string error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException("The value of a failure result can not be accessed.");

    public static Result<T> Success(T value) => new(value, true, string.Empty);
    public new static Result<T> Failure(string error) => new(default, false, error);
}
