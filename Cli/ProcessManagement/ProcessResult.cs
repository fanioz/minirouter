namespace MiniRouter.Cli.ProcessManagement;

public class ProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ProcessResult Ok() => new ProcessResult { Success = true };
    public static ProcessResult Fail(string message) => new ProcessResult { Success = false, Message = message };
}
