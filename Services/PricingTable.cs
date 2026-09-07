using System;
using System.Collections.Generic;

namespace MiniRouter.Services;

/// <summary>
/// Static pricing table for token cost calculation.
/// Four-tier lookup: exact model match > pattern match > provider-configured rate > null.
/// Unmatched models without provider rates yield null cost (not zero).
/// </summary>
public static class PricingTable
{
    // Static pricing lookup (exact match, pattern match)
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
        ("gpt-4-*", 10.00, 30.00)
    };

    /// <summary>
    /// Upper sanity bound for provider-configured rates (USD per million tokens).
    /// Rates above this bound are rejected at ingest (ProviderService) and ignored at
    /// calculation time, so a misconfigured value can never overflow the decimal
    /// cost computation (issue raised in the PR #15 review: 'negative or oversized').
    /// </summary>
    public const double MaxProviderPricePerMillionUsd = 1_000_000;

    /// <summary>
    /// Calculates the cost of a completion using a four-tier lookup strategy:
    /// exact model match > pattern match > provider-configured rate > null.
    /// Returns null if no pricing information is available.
    /// </summary>
    /// <param name="inputTokens">Number of input tokens consumed.</param>
    /// <param name="outputTokens">Number of output tokens generated.</param>
    /// <param name="providerId">Provider identifier (for logging/context).</param>
    /// <param name="modelName">Model name to look up in the pricing table.</param>
    /// <param name="providerInputRate">Optional provider-configured input rate (USD per million tokens) used as fallback when no static match exists.</param>
    /// <param name="providerOutputRate">Optional provider-configured output rate (USD per million tokens) used as fallback when no static match exists.</param>
    /// <returns>The total cost in USD, or null when the model name is empty or either rate is unavailable.</returns>
    public static decimal? CalculateCost(
        int inputTokens,
        int outputTokens,
        string providerId,
        string modelName,
        double? providerInputRate = null,
        double? providerOutputRate = null)
    {
        if (string.IsNullOrEmpty(modelName))
            return null;
        
        var inputRate = FindInputRate(modelName, providerInputRate);
        var outputRate = FindOutputRate(modelName, providerOutputRate);

        // Both rates must be found or we return null
        if (inputRate == null || outputRate == null)
            return null;

        var inputCost = (decimal)(inputTokens / 1_000_000.0 * inputRate.Value);
        var outputCost = (decimal)(outputTokens / 1_000_000.0 * outputRate.Value);
        
        return inputCost + outputCost;
    }

    /// <summary>
    /// Finds the input token rate for a model using tiered lookup: exact match, pattern match, provider-configured rate, or null.
    /// </summary>
    /// <param name="modelName">The model name to look up.</param>
    /// <param name="providerRate">Optional provider-configured input rate (USD per million tokens) used as fallback.</param>
    /// <summary>
    /// Resolves the input token rate for a model.
    /// </summary>
    /// <param name="modelName">The model name to match.</param>
    /// <param name="providerRate">The provider-configured input rate to use when no model rate matches.</param>
    /// <returns>The input rate per million tokens, or <c>null</c> when no rate is available.</returns>
    private static double? FindInputRate(string modelName, double? providerRate)
    {
        // Tier 1: Exact match
        foreach (var rate in _rates)
        {
            if (modelName == rate.Model)
                return rate.InputRatePerMillion;
        }

        // Tier 2: Pattern match
        foreach (var rate in _rates)
        {
            if (rate.Model.EndsWith("*"))
            {
                var prefix = rate.Model.Substring(0, rate.Model.Length - 1);
                if (modelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return rate.InputRatePerMillion;
            }
        }

        // Tier 3: Provider-configured rate (bounded so a stray oversized value can
        // never overflow the decimal cost computation)
        if (providerRate is >= 0 and <= MaxProviderPricePerMillionUsd)
            return providerRate.Value;

        // Tier 4: No match
        return null;
    }

    /// <summary>
    /// Finds the output token rate for a model using tiered lookup: exact match, pattern match, provider-configured rate, or null.
    /// </summary>
    /// <param name="modelName">The model name to look up.</param>
    /// <param name="providerRate">Optional provider-configured output rate (USD per million tokens) used as fallback.</param>
    /// <returns>The output rate per million tokens, or <c>null</c> if no rate is available.</returns>
    private static double? FindOutputRate(string modelName, double? providerRate)
    {
        // Tier 1: Exact match
        foreach (var rate in _rates)
        {
            if (modelName == rate.Model)
                return rate.OutputRatePerMillion;
        }

        // Tier 2: Pattern match
        foreach (var rate in _rates)
        {
            if (rate.Model.EndsWith("*"))
            {
                var prefix = rate.Model.Substring(0, rate.Model.Length - 1);
                if (modelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return rate.OutputRatePerMillion;
            }
        }

        // Tier 3: Provider-configured rate (same bound as the input lookup)
        if (providerRate is >= 0 and <= MaxProviderPricePerMillionUsd)
            return providerRate.Value;

        // Tier 4: No match
        return null;
    }
}
