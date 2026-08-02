namespace VK.API.Common;

public enum ServiceResultStatus { Success, NotFound, BadRequest, Forbidden, Error }

public class ServiceResult{
    public ServiceResultStatus Status { get; init; }
    public string? Message { get; init; }

    public static ServiceResult Success() => new() { Status = ServiceResultStatus.Success };
    public static ServiceResult NotFound(string message) => new() { Status = ServiceResultStatus.NotFound, Message = message };
    public static ServiceResult BadRequest(string message) => new() { Status = ServiceResultStatus.BadRequest, Message = message };
    public static ServiceResult Forbidden(string message) => new() { Status = ServiceResultStatus.Forbidden, Message = message };
    public static ServiceResult Error(string message) => new() { Status = ServiceResultStatus.Error, Message = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Success(T data) => new() { Status = ServiceResultStatus.Success, Data = data };
    public static new ServiceResult<T> NotFound(string message) => new() { Status = ServiceResultStatus.NotFound, Message = message };
    public static new ServiceResult<T> BadRequest(string message) => new() { Status = ServiceResultStatus.BadRequest, Message = message };
    public static new ServiceResult<T> Forbidden(string message) => new() { Status = ServiceResultStatus.Forbidden, Message = message };
    public static new ServiceResult<T> Error(string message) => new() { Status = ServiceResultStatus.Error, Message = message };
}