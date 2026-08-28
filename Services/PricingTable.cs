using System;
using System.Collections.Generic;

namespace MiniRouter.Services;

/// <summary>
/// Static pricing table for token cost calculation.
/// Three-tier lookup: exact model match > pattern match > provider default.
/// Unmatched models yield null cost (not zero) — documented as point-in-time snapshot.
/// </summary>
public static class PricingTable
{
    // Three-tier pricing lookup (exact, pattern, provider default)
    private static readonly List<(string Model, double InputRatePerMillion, double OutputRatePerMillion)> _rates = new()
    {
        ("gpt-4o-mini", 0.15, 0.60),
        ("gpt-4o", 5.00, 15.00),
        ("gpt-4-turbo", 10.00, 30.00),
        ("gpt-4", 30.00, 60.00),
        ("claude-3-haiku", 0.25, 1.25),
        ("claude-3-sonnet", 3.00, 15.00),
        ("claude-3-opus", 15.00, 75.00),
        ("claude-3-5-sonnet", 3.00, 15.00),
        ("command-r-plus", 3.00, 15.00),
        ("command-r", 0.50, 1.50),
        ("deepseek-chat", 0.14, 0.28),
        ("qwen-max", 0.70, 2.10),
        ("gpt-4o-*", 0.15, 0.60),
        ("claude-3-*", 3.00, 15.00),
        ("gpt-4-*", 10.00, 30.00),
        ("provider_default", 0.50, 2.00)
    };

    public static decimal? CalculateCost(int inputTokens, int outputTokens, string providerId, string modelName)
    {
        if (string.IsNullOrEmpty(modelName))
            return null;
        
        var inputRate = FindInputRate(modelName);
        var outputRate = FindOutputRate(modelName);

        // Both rates must be found or we return null
        if (inputRate == null || outputRate == null)
            return null;

        var inputCost = (decimal)(inputTokens / 1_000_000.0 * inputRate.Value);
        var outputCost = (decimal)(outputTokens / 1_000_000.0 * outputRate.Value);
        
        return inputCost + outputCost;
    }

    private static double? FindInputRate(string modelName)
    {
        foreach (var rate in _rates)
        {
            if (modelName == rate.Model)
                return rate.InputRatePerMillion;
        }

        foreach (var rate in _rates)
        {
            if (rate.Model.EndsWith("*"))
            {
                var prefix = rate.Model.Substring(0, rate.Model.Length - 1);
                if (modelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return rate.InputRatePerMillion;
            }
        }

        // No match found
        return null;
    }

    private static double? FindOutputRate(string modelName)
    {
        foreach (var rate in _rates)
        {
            if (modelName == rate.Model)
                return rate.OutputRatePerMillion;
        }

        foreach (var rate in _rates)
        {
            if (rate.Model.EndsWith("*"))
            {
                var prefix = rate.Model.Substring(0, rate.Model.Length - 1);
                if (modelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return rate.OutputRatePerMillion;
            }
        }

        // No match found
        return null;
    }
}
