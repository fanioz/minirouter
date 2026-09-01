using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    /// <summary>
    /// Tests for the chain-resolution intercept in ProxyService (Story 13.1 AC 5, 6).
    /// ExecuteExplicitChainAsync is private, so tests drive through ExecuteCompletionAsync.
    /// </summary>
    public class ModelChainRoutingTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        private static ProxyService BuildService(
            Mock<IModelChainService> chainMock,
            Mock<IProviderService>? providerMock = null,
            Mock<IHttpClientFactory>? factoryMock = null,
            Mock<ILogService>? logMock = null)
        {
            providerMock ??= new Mock<IProviderService>();
            factoryMock ??= new Mock<IHttpClientFactory>();
            logMock ??= new Mock<ILogService>();

            // Default: log calls succeed silently
            logMock.Setup(l => l.LogRequestAsync(It.IsAny<RequestLog>())).Returns(Task.CompletedTask);

            return new ProxyService(
                providerMock.Object,
                factoryMock.Object,
                logMock.Object,
                chainMock.Object
            );
        }

        /// <summary>
        /// Creates a fake IHttpClientFactory that always returns the given status code.
        /// </summary>
        private static IHttpClientFactory FakeFactory(HttpStatusCode statusCode, string body = "{}")
        {
            var handler = new FixedResponseHandler(statusCode, body);
            var client = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("upstream")).Returns(client);
            return factory.Object;
        }

        private static ProxyExecutionRequest ChainRequest(string model) => new ProxyExecutionRequest
        {
            RequestedModel = model,
            RequestBodyJson = $"{{\"model\":\"{model}\",\"messages\":[{{\"role\":\"user\",\"content\":\"hi\"}}]}}",
            IsStream = false,
            ContentType = "application/json"
        };

        private static MaskedProvider FakeMasked(string id) =>
            new MaskedProvider(id, id, "http://fake.local", null, true, null, null, null, null, null);

        private static Provider FakeProvider(string id) =>
            new Provider(id, id, "http://fake.local", "key", true, null, null, null, null, null);

        // ── 9.1 — chain name resolves to waterfall, first target succeeds ──────

        [Fact]
        public async Task ExecuteCompletion_ChainName_ResolvesToWaterfall_FirstTargetSucceeds()
        {
            var chainMock = new Mock<IModelChainService>();
            chainMock.Setup(c => c.GetChain("tier1")).Returns(
                new ModelChain("tier1", null, new List<string> { "pA/m1", "pB/m2" })
            );

            var providerMock = new Mock<IProviderService>();
            providerMock.Setup(p => p.GetProviderByIdAsync("pA")).ReturnsAsync(FakeMasked("pA"));
            providerMock.Setup(p => p.GetProviderByIdUnmaskedAsync("pA")).ReturnsAsync(FakeProvider("pA"));
            providerMock.Setup(p => p.GetCircuitStatus("pA", "m1")).Returns(CircuitStatus.Healthy);
            providerMock.Setup(p => p.RecordSuccess("pA", "m1"));

            var logMock = new Mock<ILogService>();
            logMock.Setup(l => l.LogRequestAsync(It.IsAny<RequestLog>())).Returns(Task.CompletedTask);

            var service = BuildService(chainMock, providerMock,
                new Mock<IHttpClientFactory>() { CallBase = false },
                logMock);

            // Inject a factory that returns 200 for pA
            var handler = new FixedResponseHandler(HttpStatusCode.OK,
                "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}");
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://fake.local") };
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("upstream")).Returns(client);

            var svc = new ProxyService(providerMock.Object, factoryMock.Object, logMock.Object, chainMock.Object);

            var result = await svc.ExecuteCompletionAsync(ChainRequest("tier1"));

            Assert.True(result.Success);
            Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
            // pB should NOT have been consulted
            providerMock.Verify(p => p.GetProviderByIdAsync("pB"), Times.Never);
        }

        // ── 9.2 — first target 503, falls back to second ──────────────────────

        [Fact]
        public async Task ExecuteCompletion_ChainName_FallsBackOnFailure()
        {
            var chainMock = new Mock<IModelChainService>();
            chainMock.Setup(c => c.GetChain("tier1")).Returns(
                new ModelChain("tier1", null, new List<string> { "pA/m1", "pB/m2" })
            );

            var providerMock = new Mock<IProviderService>();
            // Both providers are visible and healthy
            providerMock.Setup(p => p.GetProviderByIdAsync("pA")).ReturnsAsync(FakeMasked("pA"));
            providerMock.Setup(p => p.GetProviderByIdUnmaskedAsync("pA")).ReturnsAsync(FakeProvider("pA"));
            providerMock.Setup(p => p.GetCircuitStatus("pA", "m1")).Returns(CircuitStatus.Healthy);

            providerMock.Setup(p => p.GetProviderByIdAsync("pB")).ReturnsAsync(FakeMasked("pB"));
            providerMock.Setup(p => p.GetProviderByIdUnmaskedAsync("pB")).ReturnsAsync(FakeProvider("pB"));
            providerMock.Setup(p => p.GetCircuitStatus("pB", "m2")).Returns(CircuitStatus.Healthy);
            providerMock.Setup(p => p.RecordSuccess("pB", "m2"));

            var logMock = new Mock<ILogService>();
            logMock.Setup(l => l.LogRequestAsync(It.IsAny<RequestLog>())).Returns(Task.CompletedTask);

            // First call → 503, second call → 200
            int callCount = 0;
            var handler = new CallbackHttpMessageHandler(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}")
                };
            });

            var client = new HttpClient(handler);
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("upstream")).Returns(client);

            var svc = new ProxyService(providerMock.Object, factoryMock.Object, logMock.Object, chainMock.Object);

            var result = await svc.ExecuteCompletionAsync(ChainRequest("tier1"));

            Assert.True(result.Success);
            Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
            Assert.Equal(2, callCount);
        }

        // ── 9.3 — first target 400 (client error), stops immediately ─────────

        [Fact]
        public async Task ExecuteCompletion_ChainName_StopsOnClientError()
        {
            var chainMock = new Mock<IModelChainService>();
            chainMock.Setup(c => c.GetChain("tier1")).Returns(
                new ModelChain("tier1", null, new List<string> { "pA/m1", "pB/m2" })
            );

            var providerMock = new Mock<IProviderService>();
            providerMock.Setup(p => p.GetProviderByIdAsync("pA")).ReturnsAsync(FakeMasked("pA"));
            providerMock.Setup(p => p.GetProviderByIdUnmaskedAsync("pA")).ReturnsAsync(FakeProvider("pA"));
            providerMock.Setup(p => p.GetCircuitStatus("pA", "m1")).Returns(CircuitStatus.Healthy);

            var logMock = new Mock<ILogService>();
            logMock.Setup(l => l.LogRequestAsync(It.IsAny<RequestLog>())).Returns(Task.CompletedTask);

            int callCount = 0;
            var handler = new CallbackHttpMessageHandler(_ =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"bad\"}") };
            });

            var client = new HttpClient(handler);
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("upstream")).Returns(client);

            var svc = new ProxyService(providerMock.Object, factoryMock.Object, logMock.Object, chainMock.Object);

            var result = await svc.ExecuteCompletionAsync(ChainRequest("tier1"));

            Assert.False(result.Success);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            // Only one HTTP call should have been made — no fallback on 4xx
            Assert.Equal(1, callCount);
            providerMock.Verify(p => p.GetProviderByIdAsync("pB"), Times.Never);
        }

        // ── 9.4 — all 3 targets 503, returns last error ───────────────────────

        [Fact]
        public async Task ExecuteCompletion_ChainName_AllFail_ReturnsLastError()
        {
            var chainMock = new Mock<IModelChainService>();
            chainMock.Setup(c => c.GetChain("tier1")).Returns(
                new ModelChain("tier1", null, new List<string> { "pA/m1", "pB/m2", "pC/m3" })
            );

            var providerMock = new Mock<IProviderService>();
            foreach (var (pid, mid) in new[] { ("pA", "m1"), ("pB", "m2"), ("pC", "m3") })
            {
                var (p, m) = (pid, mid);
                providerMock.Setup(s => s.GetProviderByIdAsync(p)).ReturnsAsync(FakeMasked(p));
                providerMock.Setup(s => s.GetProviderByIdUnmaskedAsync(p)).ReturnsAsync(FakeProvider(p));
                providerMock.Setup(s => s.GetCircuitStatus(p, m)).Returns(CircuitStatus.Healthy);
            }

            var logMock = new Mock<ILogService>();
            logMock.Setup(l => l.LogRequestAsync(It.IsAny<RequestLog>())).Returns(Task.CompletedTask);

            int callCount = 0;
            var handler = new CallbackHttpMessageHandler(_ =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"error\":\"down\"}") };
            });

            var client = new HttpClient(handler);
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("upstream")).Returns(client);

            var svc = new ProxyService(providerMock.Object, factoryMock.Object, logMock.Object, chainMock.Object);

            var result = await svc.ExecuteCompletionAsync(ChainRequest("tier1"));

            Assert.False(result.Success);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            Assert.Equal(3, callCount);
        }

        // ── 9.5 — empty chain returns 400 ─────────────────────────────────────

        [Fact]
        public async Task ExecuteCompletion_EmptyChain_Returns400()
        {
            var chainMock = new Mock<IModelChainService>();
            chainMock.Setup(c => c.GetChain("empty")).Returns(
                new ModelChain("empty", null, new List<string>())
            );

            var svc = BuildService(chainMock);
            var result = await svc.ExecuteCompletionAsync(ChainRequest("empty"));

            Assert.False(result.Success);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        // ── 9.6 — unknown model name (not a chain, no '/') → round-robin path ─

        [Fact]
        public async Task ExecuteCompletion_UnknownName_FallsToRoundRobin()
        {
            var chainMock = new Mock<IModelChainService>();
            // Returns null → not a chain
            chainMock.Setup(c => c.GetChain("gpt-4o")).Returns((ModelChain?)null);

            var providerMock = new Mock<IProviderService>();
            // No providers available → 503 from round-robin path
            providerMock
                .Setup(s => s.GetNextProviderAsync("gpt-4o", It.IsAny<HashSet<string>?>()))
                .ReturnsAsync((Provider?)null);

            var svc = BuildService(chainMock, providerMock);
            var result = await svc.ExecuteCompletionAsync(ChainRequest("gpt-4o"));

            // Round-robin returns 503 "No enabled providers" — NOT 400 "must be providerId/modelName"
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
            providerMock.Verify(
                s => s.GetNextProviderAsync("gpt-4o", It.IsAny<HashSet<string>?>()),
                Times.AtLeastOnce
            );
        }
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    internal sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public FixedResponseHandler(HttpStatusCode status, string body = "{}")
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    internal sealed class CallbackHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _callback;

        public CallbackHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> callback)
        {
            _callback = callback;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_callback(request));
        }
    }
}
