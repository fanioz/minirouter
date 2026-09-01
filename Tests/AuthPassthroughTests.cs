using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using MiniRouter.Filters;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    /// <summary>
    /// Tests for AUTH_PASSTHROUGH mode and x-api-key header support (Story 10.1 AC 1–3).
    /// </summary>
    public class AuthPassthroughTests
    {
        private readonly Mock<IApiKeyService> _apiKeyServiceMock;

        public AuthPassthroughTests()
        {
            _apiKeyServiceMock = new Mock<IApiKeyService>();
        }

        private static ApiKeyEndpointFilter CreateFilter(bool passthroughEnabled)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AUTH_PASSTHROUGH"] = passthroughEnabled.ToString().ToLower()
                })
                .Build();

            return new ApiKeyEndpointFilter(new Mock<IApiKeyService>().Object, config);
        }

        private static EndpointFilterInvocationContext CreateContext(string? authHeader = null, string? xApiKey = null)
        {
            var httpContext = new DefaultHttpContext();
            if (authHeader != null)
                httpContext.Request.Headers.Authorization = authHeader;
            if (xApiKey != null)
                httpContext.Request.Headers["x-api-key"] = xApiKey;

            var invocation = new Mock<EndpointFilterInvocationContext>();
            invocation.SetupGet(c => c.HttpContext).Returns(httpContext);
            invocation.SetupGet(c => c.Arguments).Returns(new List<object?>());
            return invocation.Object;
        }

        // --- AUTH_PASSTHROUGH=true tests (AC 2) ---

        [Fact]
        public async Task AuthPassthrough_Enabled_AnyToken_Proceeds_SetsPassthroughId()
        {
            var filter = CreateFilter(passthroughEnabled: true);
            var ctx = CreateContext(authHeader: "Bearer sk-totally-unknown-key");

            var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            Assert.Equal("next", result);
            Assert.Equal("passthrough", ctx.HttpContext.Items["ApiKeyId"]);
        }

        [Fact]
        public async Task AuthPassthrough_Enabled_MissingToken_Proceeds_SetsPassthroughId()
        {
            var filter = CreateFilter(passthroughEnabled: true);
            // No auth header at all — remote IP won't be loopback, but passthrough skips that check
            var ctx = CreateContext();

            var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            Assert.Equal("next", result);
            Assert.Equal("passthrough", ctx.HttpContext.Items["ApiKeyId"]);
        }

        // --- AUTH_PASSTHROUGH=false / absent tests (AC 3) ---

        [Fact]
        public async Task AuthPassthrough_Disabled_UnknownKey_ReturnsUnauthorized()
        {
            var filter = CreateFilter(passthroughEnabled: false);

            var config = new ConfigurationBuilder().Build(); // no AUTH_PASSTHROUGH key
            var filterNoKey = new ApiKeyEndpointFilter(_apiKeyServiceMock.Object, config);
            _apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-unknown"))
                .ReturnsAsync((ApiKey?)null);

            var ctx = CreateContext(authHeader: "Bearer sk-unknown");
            var result = await filterNoKey.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        // --- x-api-key header tests (AC 1) ---

        [Fact]
        public async Task XApiKeyHeader_ValidKey_ProceedsAndSetsApiKeyId()
        {
            var apiKeyServiceMock = new Mock<IApiKeyService>();
            apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-xvalid"))
                .ReturnsAsync(new ApiKey { Id = "xkey1", Enabled = true });

            var config = new ConfigurationBuilder().Build();
            var filter = new ApiKeyEndpointFilter(apiKeyServiceMock.Object, config);

            var ctx = CreateContext(xApiKey: "sk-xvalid");
            var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            Assert.Equal("next", result);
            Assert.Equal("xkey1", ctx.HttpContext.Items["ApiKeyId"]);
        }

        [Fact]
        public async Task XApiKeyHeader_UnknownKey_ReturnsUnauthorized()
        {
            var apiKeyServiceMock = new Mock<IApiKeyService>();
            apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-xunknown"))
                .ReturnsAsync((ApiKey?)null);

            var config = new ConfigurationBuilder().Build();
            var filter = new ApiKeyEndpointFilter(apiKeyServiceMock.Object, config);

            var ctx = CreateContext(xApiKey: "sk-xunknown");
            var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        [Fact]
        public async Task XApiKeyHeader_DisabledKey_ReturnsUnauthorized()
        {
            var apiKeyServiceMock = new Mock<IApiKeyService>();
            apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-xdisabled"))
                .ReturnsAsync(new ApiKey { Id = "xkey2", Enabled = false });

            var config = new ConfigurationBuilder().Build();
            var filter = new ApiKeyEndpointFilter(apiKeyServiceMock.Object, config);

            var ctx = CreateContext(xApiKey: "sk-xdisabled");
            var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        }

        [Fact]
        public async Task BearerHeader_TakesPrecedenceOver_XApiKey()
        {
            // When both headers are present, Bearer wins
            var apiKeyServiceMock = new Mock<IApiKeyService>();
            apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-bearer"))
                .ReturnsAsync(new ApiKey { Id = "bearer-key", Enabled = true });
            apiKeyServiceMock.Setup(s => s.ValidateAndRecordUsageAsync("sk-xkey"))
                .ReturnsAsync(new ApiKey { Id = "xkey", Enabled = true });

            var config = new ConfigurationBuilder().Build();
            var filter = new ApiKeyEndpointFilter(apiKeyServiceMock.Object, config);

            var ctx = CreateContext(authHeader: "Bearer sk-bearer", xApiKey: "sk-xkey");
            var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>("next"));

            Assert.Equal("next", result);
            Assert.Equal("bearer-key", ctx.HttpContext.Items["ApiKeyId"]);
        }
    }
}
