using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MiniRouter.Models;

namespace MiniRouter.Services;

public interface IProxyService
{
    Task HandleChatCompletionAsync(HttpContext ctx);
    Task HandleAnthropicMessagesAsync(HttpContext ctx);
    Task<ProxyExecutionResult> ExecuteCompletionAsync(ProxyExecutionRequest request, System.Threading.CancellationToken ct = default);
}
