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

        public ProxyServiceTests()
        {
            _providerServiceMock = new Mock<IProviderService>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _logServiceMock = new Mock<ILogService>();
        }

        [Fact]
        public async Task HandleChatCompletionAsync_MissingModel_Returns400()
        {
            var service = new ProxyService(
                _providerServiceMock.Object,
                _httpClientFactoryMock.Object,
                _logServiceMock.Object
            );

            var context = new DefaultHttpContext();
            context.Items["ApiKeyId"] = "key1";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            context.Response.Body = new MemoryStream();

            await service.HandleChatCompletionAsync(context);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        }
    }
}
