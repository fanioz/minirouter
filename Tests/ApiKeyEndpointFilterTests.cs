using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using MiniRouter.Filters;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    public class ApiKeyEndpointFilterTests
    {
        private readonly Mock<IApiKeyService> _apiKeyServiceMock;
        private readonly ApiKeyEndpointFilter _filter;

        public ApiKeyEndpointFilterTests()
        {
            _apiKeyServiceMock = new Mock<IApiKeyService>();
            _filter = new ApiKeyEndpointFilter(_apiKeyServiceMock.Object);
        }

        private static EndpointFilterInvocationContext CreateContext(string? authHeader)
        {
            var httpContext = new DefaultHttpContext();
            if (authHeader != null)
            {
                httpContext.Request.Headers.Authorization = authHeader;
            }

            var invocation = new Mock<EndpointFilterInvocationContext>();
            invocation.SetupGet(c => c.HttpContext).Returns(httpContext);
            invocation.SetupGet(c => c.Arguments).Returns(new List<object?>());
            return invocation.Object;
        }

        [Fact]
        public async Task MissingAuthorizationHeader_ReturnsUnauthorized()
        {
            var ctx = CreateContext(null);
            var result = await _filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        [Fact]
        public async Task MalformedHeader_NoBearerPrefix_ReturnsUnauthorized()
        {
            var ctx = CreateContext("sk-notabearer");
            var result = await _filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        [Fact]
        public async Task UnknownKey_ReturnsUnauthorized()
        {
            _apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-unknown"))
                .ReturnsAsync((ApiKey?)null);

            var ctx = CreateContext("Bearer sk-unknown");
            var result = await _filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        [Fact]
        public async Task DisabledKey_ReturnsUnauthorized()
        {
            _apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-disabled"))
                .ReturnsAsync(new ApiKey { Id = "key1", Enabled = false });

            var ctx = CreateContext("Bearer sk-disabled");
            var result = await _filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        [Fact]
        public async Task ValidKey_ProceedsAndRecordsApiKeyId()
        {
            _apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-valid"))
                .ReturnsAsync(new ApiKey { Id = "key1", Enabled = true });

            var ctx = CreateContext("Bearer sk-valid");
            var result = await _filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            Assert.Equal("next", result);
            Assert.Equal("key1", ctx.HttpContext.Items["ApiKeyId"]);
        }
    }
}
