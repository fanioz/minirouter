using System;
using System.Threading.Tasks;
using Spectre.Console;

namespace MiniRouter.Cli.ProcessManagement;

public class TerminalFeedback : ITerminalFeedback
{
    public void WriteState(string state, string colorToken)
    {
        AnsiConsole.MarkupLine($"[{colorToken}]{state}[/]");
    }

    public void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]❌ {message}[/]");
    }

    public Task<T> RunWithSpinnerAsync<T>(string message, Func<Task<T>> action)
    {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync($"[blue]{message}[/]", async ctx =>
            {
                return await action();
            });
    }

    public Task RunWithSpinnerAsync(string message, Func<Task> action)
    {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync($"[blue]{message}[/]", async ctx =>
            {
                await action();
            });
    }
}
