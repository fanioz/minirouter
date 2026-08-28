using System;
using System.Threading.Tasks;

namespace MiniRouter.Cli.ProcessManagement;

public interface ITerminalFeedback
{
    void WriteState(string state, string colorToken);
    void WriteError(string message);
    Task<T> RunWithSpinnerAsync<T>(string message, Func<Task<T>> action);
    Task RunWithSpinnerAsync(string message, Func<Task> action);
}
