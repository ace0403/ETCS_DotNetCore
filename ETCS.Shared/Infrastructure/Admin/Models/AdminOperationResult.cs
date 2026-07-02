namespace ETCS.Shared.Infrastructure.Admin.Models;

public sealed class AdminOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static AdminOperationResult Ok(string message) => new() { Success = true, Message = message };
    public static AdminOperationResult Fail(string message) => new() { Success = false, Message = message };
}
