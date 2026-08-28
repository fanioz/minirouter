using System.Globalization;

namespace MiniRouter.Services;

public enum ErrorKind
{
    Fixed,
    Backoff
}

public record ErrorClassification(ErrorKind Kind, TimeSpan Cooldown);

public static class ErrorClassifier
{
    public const int BackoffBaseMs = 2000;
    public const int BackoffMaxMs = 5 * 60 * 1000;
    public const int BackoffMaxLevel = 15;
    public const int TransientCooldownMs = 30 * 1000;
    public const int MaxRateLimitCooldownMs = 30 * 60 * 1000;

    private const int CooldownLongMs = 2 * 60 * 1000;
    private const int CooldownShortMs = 5 * 1000;

    private record TextRule(string Text, ErrorKind Kind, int CooldownMs);
    private record StatusRule(int Status, ErrorKind Kind, int CooldownMs);

    private static readonly TextRule[] TextRules =
    {
        new("no credentials", ErrorKind.Fixed, CooldownLongMs),
        new("request not allowed", ErrorKind.Fixed, CooldownShortMs),
        new("improperly formed request", ErrorKind.Fixed, CooldownLongMs),
        new("rate limit", ErrorKind.Backoff, 0),
        new("too many requests", ErrorKind.Backoff, 0),
        new("quota exceeded", ErrorKind.Backoff, 0),
        new("capacity", ErrorKind.Backoff, 0),
        new("overloaded", ErrorKind.Backoff, 0),
    };

    private static readonly StatusRule[] StatusRules =
    {
        new(401, ErrorKind.Fixed, CooldownLongMs),
        new(402, ErrorKind.Fixed, CooldownLongMs),
        new(403, ErrorKind.Fixed, CooldownLongMs),
        new(404, ErrorKind.Fixed, CooldownLongMs),
        new(429, ErrorKind.Backoff, 0),
    };

    public static ErrorClassification Classify(int status, string? errorText)
    {
        var lower = errorText?.ToLowerInvariant() ?? string.Empty;

        foreach (var rule in TextRules)
        {
            if (lower.Contains(rule.Text, StringComparison.Ordinal))
            {
                return new ErrorClassification(rule.Kind, rule.Kind == ErrorKind.Fixed ? TimeSpan.FromMilliseconds(rule.CooldownMs) : TimeSpan.Zero);
            }
        }

        foreach (var rule in StatusRules)
        {
            if (rule.Status == status)
            {
                return new ErrorClassification(rule.Kind, rule.Kind == ErrorKind.Fixed ? TimeSpan.FromMilliseconds(rule.CooldownMs) : TimeSpan.Zero);
            }
        }

        return new ErrorClassification(ErrorKind.Fixed, TimeSpan.FromMilliseconds(TransientCooldownMs));
    }

    public static TimeSpan ComputeBackoffCooldown(int backoffLevel)
    {
        var level = Math.Clamp(backoffLevel - 1, 0, BackoffMaxLevel);
        var cooldownMs = BackoffBaseMs * Math.Pow(2, level);
        return TimeSpan.FromMilliseconds(Math.Min(cooldownMs, BackoffMaxMs));
    }

    public static int NextBackoffLevel(int current) => Math.Min(current + 1, BackoffMaxLevel);

    public static TimeSpan? ParseRetryAfter(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (int.TryParse(trimmed, out var seconds) && seconds >= 0)
            return TimeSpan.FromSeconds(seconds);

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
        {
            var diff = date - now;
            return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
        }

        return null;
    }

    public static TimeSpan? ParseResetsAt(string? errorBody, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
            return null;

        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(errorBody);
            var resetsAt = node?["resets_at"]
                ?? node?["usage_limit_reached"]?["resets_at"]
                ?? node?["error"]?["usage_limit_reached"]?["resets_at"];
            if (resetsAt == null)
                return null;

            if (resetsAt.GetValueKind() == System.Text.Json.JsonValueKind.Number)
            {
                var num = resetsAt.GetValue<double>();
                DateTimeOffset ts;
                if (num > 1e12)
                    ts = DateTimeOffset.FromUnixTimeMilliseconds((long)num);
                else
                    ts = DateTimeOffset.FromUnixTimeSeconds((long)num);
                var diff = ts - now;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }

            var str = resetsAt.ToString();
            if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                var diff = parsed - now;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }
        }
        catch (System.Text.Json.JsonException) { }

        return null;
    }

    public static TimeSpan? ResolveProviderReportedReset(string? retryAfterHeader, string? errorBody, DateTimeOffset now)
    {
        var candidate = ParseRetryAfter(retryAfterHeader, now) ?? ParseResetsAt(errorBody, now);
        if (candidate == null)
            return null;

        var max = TimeSpan.FromMilliseconds(MaxRateLimitCooldownMs);
        return candidate > max ? max : candidate;
    }
}
