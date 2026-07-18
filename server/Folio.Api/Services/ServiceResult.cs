namespace Folio.Api.Services;

public enum OperationStatus
{
    Success,
    NotFound,
    Invalid,
    Forbidden,
    Conflict,
}

/// <summary>Carries an operation outcome plus a value or an error message.</summary>
public record ServiceResult<T>(OperationStatus Status, T? Value, string? Error)
{
    public static ServiceResult<T> Ok(T value) => new(OperationStatus.Success, value, null);
    public static ServiceResult<T> NotFound(string? error = null) => new(OperationStatus.NotFound, default, error);
    public static ServiceResult<T> Invalid(string error) => new(OperationStatus.Invalid, default, error);
    public static ServiceResult<T> Forbidden(string? error = null) => new(OperationStatus.Forbidden, default, error);
    public static ServiceResult<T> Conflict(string error) => new(OperationStatus.Conflict, default, error);
}
