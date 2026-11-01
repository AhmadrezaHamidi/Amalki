namespace Amlak.Application;

/// <summary>
/// The outcome of a use case: done (with a value), not found, or refused because a business rule was broken.
/// <see cref="Error"/> is the Persian message of the <see cref="DomainException"/>, ready to show to the user.
/// </summary>
public readonly record struct Result<T>
{
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool Succeeded => Error is null && !IsNotFound;

    public static Result<T> Ok(T value) => new() { Value = value };
    public static Result<T> Fail(string error) => new() { Error = error };
    public static Result<T> NotFound() => new() { IsNotFound = true };

    public static implicit operator Result<T>(T value) => Ok(value);
}

/// <summary>A use case result without a value.</summary>
public readonly record struct Result
{
    public string? Error { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool Succeeded => Error is null && !IsNotFound;

    public static Result Ok() => new();
    public static Result Fail(string error) => new() { Error = error };
    public static Result NotFound() => new() { IsNotFound = true };
}
