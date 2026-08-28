using System;
using Xunit;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    public class ErrorClassifierTests
    {
        // --- Text rules (checked before status) ---

        [Fact]
        public void Classify_NoCredentials_ReturnsFixed2Min()
        {
            var result = ErrorClassifier.Classify(400, "no credentials configured");
            Assert.Equal(ErrorKind.Fixed, result.Kind);
            Assert.Equal(TimeSpan.FromMinutes(2), result.Cooldown);
        }

        [Fact]
        public void Classify_RequestNotAllowed_ReturnsFixed5Sec()
        {
            var result = ErrorClassifier.Classify(400, "request not allowed for this model");
            Assert.Equal(ErrorKind.Fixed, result.Kind);
            Assert.Equal(TimeSpan.FromSeconds(5), result.Cooldown);
        }

        [Fact]
        public void Classify_ImproperlyFormedRequest_ReturnsFixed2Min()
        {
            var result = ErrorClassifier.Classify(400, "improperly formed request");
            Assert.Equal(ErrorKind.Fixed, result.Kind);
            Assert.Equal(TimeSpan.FromMinutes(2), result.Cooldown);
        }

        [Theory]
        [InlineData("rate limit exceeded")]
        [InlineData("too many requests")]
        [InlineData("quota exceeded")]
        [InlineData("capacity")]
        [InlineData("overloaded")]
        public void Classify_BackoffTextRules_ReturnBackoffKind(string text)
        {
            var result = ErrorClassifier.Classify(500, text);
            Assert.Equal(ErrorKind.Backoff, result.Kind);
        }

        [Fact]
        public void Classify_TextRuleTakesPrecedenceOverStatus()
        {
            // 500 with "overloaded" should be Backoff (text rule), not transient default (status 500 unmatched)
            var result = ErrorClassifier.Classify(500, "Service overloaded, please retry");
            Assert.Equal(ErrorKind.Backoff, result.Kind);
        }

        // --- Status rules ---

        [Theory]
        [InlineData(401)]
        [InlineData(402)]
        [InlineData(403)]
        [InlineData(404)]
        public void Classify_FixedStatusRules_ReturnFixed2Min(int status)
        {
            var result = ErrorClassifier.Classify(status, "some generic error");
            Assert.Equal(ErrorKind.Fixed, result.Kind);
            Assert.Equal(TimeSpan.FromMinutes(2), result.Cooldown);
        }

        [Fact]
        public void Classify_Status429_ReturnsBackoff()
        {
            var result = ErrorClassifier.Classify(429, "error");
            Assert.Equal(ErrorKind.Backoff, result.Kind);
        }

        // --- Transient default ---

        [Fact]
        public void Classify_Unmatched500_ReturnsTransientDefault()
        {
            var result = ErrorClassifier.Classify(500, "internal error");
            Assert.Equal(ErrorKind.Fixed, result.Kind);
            Assert.Equal(TimeSpan.FromSeconds(30), result.Cooldown);
        }

        [Fact]
        public void Classify_NullErrorText_ReturnsTransientDefault()
        {
            var result = ErrorClassifier.Classify(502, null);
            Assert.Equal(ErrorKind.Fixed, result.Kind);
            Assert.Equal(TimeSpan.FromSeconds(30), result.Cooldown);
        }

        // --- Backoff computation ---

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(3, 8)]
        [InlineData(4, 16)]
        [InlineData(5, 32)]
        [InlineData(6, 64)]
        [InlineData(7, 128)]
        [InlineData(8, 256)]
        [InlineData(9, 300)] // capped at 5 min = 300s
        [InlineData(15, 300)]
        [InlineData(16, 300)] // clamped to max level 15
        public void ComputeBackoffCooldown_Progression(int level, int expectedSeconds)
        {
            var result = ErrorClassifier.ComputeBackoffCooldown(level);
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
        }

        [Fact]
        public void NextBackoffLevel_Increments()
        {
            Assert.Equal(1, ErrorClassifier.NextBackoffLevel(0));
            Assert.Equal(15, ErrorClassifier.NextBackoffLevel(14));
            Assert.Equal(15, ErrorClassifier.NextBackoffLevel(15)); // max level
        }

        // --- Retry-After parsing ---

        [Fact]
        public void ParseRetryAfter_DeltaSeconds_ReturnsDuration()
        {
            var now = DateTimeOffset.UtcNow;
            var result = ErrorClassifier.ParseRetryAfter("300", now);
            Assert.Equal(TimeSpan.FromSeconds(300), result);
        }

        [Fact]
        public void ParseRetryAfter_HttpDate_ReturnsDuration()
        {
            var now = DateTimeOffset.UtcNow;
            var future = now.AddMinutes(5);
            var result = ErrorClassifier.ParseRetryAfter(future.ToString("R"), now);
            Assert.NotNull(result);
            Assert.True(result > TimeSpan.FromMinutes(4));
        }

        [Fact]
        public void ParseRetryAfter_Invalid_ReturnsNull()
        {
            var result = ErrorClassifier.ParseRetryAfter("not-a-date", DateTimeOffset.UtcNow);
            Assert.Null(result);
        }

        [Fact]
        public void ParseRetryAfter_Null_ReturnsNull()
        {
            Assert.Null(ErrorClassifier.ParseRetryAfter(null, DateTimeOffset.UtcNow));
        }

        // --- resets_at parsing ---

        [Fact]
        public void ParseResetsAt_UnixSeconds_ReturnsDuration()
        {
            var now = DateTimeOffset.UtcNow;
            var resetsAt = now.AddMinutes(2).ToUnixTimeSeconds();
            var result = ErrorClassifier.ParseResetsAt($"{{\"resets_at\": {resetsAt}}}", now);
            Assert.NotNull(result);
            Assert.True(result > TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void ParseResetsAt_IsoString_ReturnsDuration()
        {
            var now = DateTimeOffset.UtcNow;
            var result = ErrorClassifier.ParseResetsAt($"{{\"resets_at\": \"{now.AddMinutes(3):O}\"}}", now);
            Assert.NotNull(result);
            Assert.True(result > TimeSpan.FromMinutes(2));
        }

        [Fact]
        public void ParseResetsAt_Nested_ReturnsDuration()
        {
            var now = DateTimeOffset.UtcNow;
            var resetsAt = now.AddMinutes(2).ToUnixTimeSeconds();
            var body = $"{{\"error\": {{\"usage_limit_reached\": {{\"resets_at\": {resetsAt}}}}}}}";
            var result = ErrorClassifier.ParseResetsAt(body, now);
            Assert.NotNull(result);
        }

        [Fact]
        public void ParseResetsAt_InvalidJson_ReturnsNull()
        {
            Assert.Null(ErrorClassifier.ParseResetsAt("{broken json", DateTimeOffset.UtcNow));
        }

        // --- Provider reset resolution + cap ---

        [Fact]
        public void ResolveProviderReportedReset_CapsAt30Min()
        {
            var now = DateTimeOffset.UtcNow;
            var result = ErrorClassifier.ResolveProviderReportedReset("21600", null, now); // 6 hours
            Assert.Equal(TimeSpan.FromMinutes(30), result);
        }

        [Fact]
        public void ResolveProviderReportedReset_PrefersHeaderOverBody()
        {
            var now = DateTimeOffset.UtcNow;
            var header = ErrorClassifier.ResolveProviderReportedReset("60", $"{{\"resets_at\": {now.AddMinutes(5).ToUnixTimeSeconds()}}}", now);
            Assert.Equal(TimeSpan.FromSeconds(60), header);
        }
    }
}
