using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MiniRouter.Cli.ProcessManagement;

public class ServerProcessManager : IServerProcessManager
{
    private readonly IProcessLocator _processLocator;
    private readonly ITerminalFeedback _feedback;

    public ServerProcessManager(IProcessLocator processLocator, ITerminalFeedback feedback)
    {
        _processLocator = processLocator;
        _feedback = feedback;
    }

    public async Task<ProcessResult> RestartServerAsync(RestartOptions options, CancellationToken cancellationToken)
    {
        _feedback.WriteState("🔍 Locating active MiniRouter server...", "cyan");
        var activeProcessInfo = _processLocator.FindRunningServerProcess();
        var activeProcess = activeProcessInfo?.Process;
        var address = activeProcessInfo?.Address ?? "http://localhost:8080";

        if (activeProcess == null)
        {
            _feedback.WriteState("ℹ️ No active server found. Starting a new instance...", "yellow");
        }
        else
        {
            bool gracefullyShutdown = false;
            
            if (!options.Force)
            {
                gracefullyShutdown = await _feedback.RunWithSpinnerAsync("Requesting graceful shutdown...", async () => 
                {
                    return await TryGracefulShutdownAsync(activeProcess, address, TimeSpan.FromSeconds(5));
                });
            }

            if (!gracefullyShutdown)
            {
                if (!options.Force)
                {
                    _feedback.WriteState("⚠️ Graceful shutdown failed. Forcing termination...", "yellow");
                }
                else 
                {
                    _feedback.WriteState("⚠️ Force flag provided. Forcing termination...", "yellow");
                }
                
                try
                {
                    activeProcess.Kill(true);
                    activeProcess.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    return ProcessResult.Fail($"Failed to forcefully kill process: {ex.Message}");
                }
            }
        }

        Process? newProcess = null;
        await _feedback.RunWithSpinnerAsync("Starting new server instance...", async () => 
        {
            newProcess = StartNewServerProcess();
            await Task.Delay(1500, cancellationToken);
        });

        if (newProcess != null && !newProcess.HasExited)
        {
            if (activeProcess != null)
            {
                if (!options.Force)
                    _feedback.WriteState($"✔️ Server restarted successfully (New PID: {newProcess.Id})", "green");
                else
                    _feedback.WriteState($"✔️ Server forcefully restarted (New PID: {newProcess.Id})", "green");
            }
            else
            {
                _feedback.WriteState($"✔️ Server started (PID: {newProcess.Id})", "green");
            }
            return ProcessResult.Ok();
        }
        else
        {
            _feedback.WriteError("Failed to restart server: Process exited immediately.");
            return ProcessResult.Fail("Process exited immediately.");
        }
    }

    private async Task<bool> TryGracefulShutdownAsync(Process process, string address, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            var shutdownUrl = address.TrimEnd('/') + "/_shutdown";
            await client.PostAsync(shutdownUrl, null);
        }
        catch
        {
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (process.HasExited)
            {
                return true;
            }
            await Task.Delay(200);
        }

        return false;
    }

    private Process StartNewServerProcess()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "dotnet";
        var arguments = exePath.EndsWith("dotnet") ? "run" : "";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var newProcess = Process.Start(startInfo);
        if (newProcess == null)
            throw new Exception("Failed to spawn process");
            
        return newProcess;
    }
}
