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
    }
}
