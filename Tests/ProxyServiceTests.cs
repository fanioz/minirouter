using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    public class ProxyServiceTests
    {
        private readonly Mock<IProviderService> _providerServiceMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<ILogService> _logServiceMock;
        private readonly Mock<IModelChainService> _chainServiceMock;

        public ProxyServiceTests()
        {
            _providerServiceMock = new Mock<IProviderService>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _logServiceMock = new Mock<ILogService>();
            _chainServiceMock = new Mock<IModelChainService>();
        }

        [Fact]
        public async Task HandleChatCompletionAsync_MissingModel_Returns400()
        {
            var service = new ProxyService(
                _providerServiceMock.Object,
                _httpClientFactoryMock.Object,
                _logServiceMock.Object,
                _chainServiceMock.Object
            );

            var context = new DefaultHttpContext();
            context.Items["ApiKeyId"] = "key1";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            context.Response.Body = new MemoryStream();

            await service.HandleChatCompletionAsync(context);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        }

        [Fact]
        public async Task ExecuteCompletionAsync_NoSlashModel_CallsRoundRobinPath()
        {
            // Story 10.1 AC 6: model names without '/' should route via round-robin,
            // not the explicit-provider path (which requires providerId/modelName).
            var service = new ProxyService(
                _providerServiceMock.Object,
                _httpClientFactoryMock.Object,
                _logServiceMock.Object,
                _chainServiceMock.Object
            );

            // No enabled providers for "gpt-4o" → returns 503 (not 400 "must be specified as providerId/modelName")
            _providerServiceMock
                .Setup(s => s.GetNextProviderAsync("gpt-4o", It.IsAny<HashSet<string>?>()))
                .ReturnsAsync((Provider?)null);

            var request = new ProxyExecutionRequest
            {
                RequestedModel = "gpt-4o",   // no slash — should go round-robin
                RequestBodyJson = "{\"model\":\"gpt-4o\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
                IsStream = false
            };

            var result = await service.ExecuteCompletionAsync(request);

            // Must NOT be 400 (which would mean the slash-required explicit path was hit)
            Assert.NotEqual(StatusCodes.Status400BadRequest, result.StatusCode);
            // Should be 503 because no providers are configured for this model
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            // Verify the round-robin path was invoked
            _providerServiceMock.Verify(
                s => s.GetNextProviderAsync("gpt-4o", It.IsAny<HashSet<string>?>()),
                Times.AtLeastOnce);
        }

        // ── /v1/messages non-stream (issue #18: content-type + raw fallback) ──

        private sealed class FixedResponseHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public FixedResponseHandler(HttpStatusCode status, string body, string contentType = "application/json")
            {
                _response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, contentType)
                };
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(_response);
        }

        private ProxyService BuildAnthropicService(string upstreamBody, string upstreamContentType = "application/json")
        {
            var handler = new FixedResponseHandler(HttpStatusCode.OK, upstreamBody, upstreamContentType);
            _httpClientFactoryMock.Setup(f => f.CreateClient("upstream")).Returns(new HttpClient(handler));
            _providerServiceMock
                .Setup(p => p.GetNextProviderAsync(It.IsAny<string?>(), It.IsAny<HashSet<string>?>()))
                .ReturnsAsync(new Provider("p1", "P1", "http://fake.local", "key", true, null, null, null, null, null, null, null));
            _logServiceMock.Setup(l => l.LogRequestAsync(It.IsAny<RequestLog>())).Returns(Task.CompletedTask);

            return new ProxyService(
                _providerServiceMock.Object,
                _httpClientFactoryMock.Object,
                _logServiceMock.Object,
                _chainServiceMock.Object
            );
        }

        private static DefaultHttpContext AnthropicContext(string requestBody)
        {
            var context = new DefaultHttpContext();
            context.Items["ApiKeyId"] = "key1";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static string ReadBody(DefaultHttpContext context)
        {
            context.Response.Body.Position = 0;
            return new StreamReader(context.Response.Body).ReadToEnd();
        }

        [Fact]
        public async Task HandleAnthropicMessagesAsync_NonStream_SetsApplicationJsonContentType()
        {
            var service = BuildAnthropicService(
                "{\"id\":\"chatcmpl-1\",\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}");

            var context = AnthropicContext(
                "{\"model\":\"gpt-test\",\"max_tokens\":32,\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}");

            await service.HandleAnthropicMessagesAsync(context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            using var doc = JsonDocument.Parse(ReadBody(context));
            Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
            Assert.Equal("1", doc.RootElement.GetProperty("id").GetString());
        }

        [Fact]
        public async Task HandleAnthropicMessagesAsync_MalformedUpstreamBody_PassesThroughRaw()
        {
            // A 200 with a non-JSON body (HTML block page, truncated body) must fall
            // through to raw passthrough, not throw an unhandled JsonException.
            var html = "<html>blocked by proxy</html>";
            var service = BuildAnthropicService(html, "text/html");

            var context = AnthropicContext(
                "{\"model\":\"gpt-test\",\"max_tokens\":32,\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}");

            await service.HandleAnthropicMessagesAsync(context);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(html, ReadBody(context));
        }
    }
}
