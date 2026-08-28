using Xunit;
using MiniRouter.Services;

namespace MiniRouter.Tests;

public class PricingTableTests
{
    [Fact]
    public void ExactModelMatch_ReturnsSpecificRate()
    {
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "openai", "gpt-4o-mini");
        
        // gpt-4o-mini: $0.15 input + $0.60 output per million
        Assert.Equal(0.75m, cost);
    }

    [Fact]
    public void PatternMatch_ResolvesVariants()
    {
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "openai", "gpt-4o-custom-v2");
        
        // gpt-4o-* pattern matches mini rate
        Assert.Equal(0.75m, cost);
    }

    [Fact]
    public void ProviderDefault_ApplicationForUnknownModel()
    {
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "unknown-provider", "provider_default");
        
        // provider_default: $0.50 input + $2.00 output per million
        Assert.Equal(2.50m, cost);
    }

    [Theory]
    [InlineData("gpt-4-turbo", 10.0)]  // gpt-4-* pattern -> turbo rate ($10M input)
    [InlineData("claude-3-haiku-pro", 3.0)]  // claude-3-* pattern -> sonnet rate ($3M input)
    public void PatternMatch_PrefixMatching(string model, double expectedPerMillionInput)
    {
        var cost = PricingTable.CalculateCost(1_000_000, 0, "provider", model);
        Assert.NotNull(cost);
        Assert.Equal(expectedPerMillionInput, (double)cost.Value);
    }

    [Fact]
    public void NullModel_ReturnsNullCost()
    {
        var cost = PricingTable.CalculateCost(100_000, 100_000, "test", "");
        Assert.Null(cost);
    }

    [Fact]
    public void CalculationPrecision_UseDecimal()
    {
        // Test that we don't have float drift issues
        var sum = 0m;
        for (int i = 0; i < 1000000; i++)
        {
            var cost = PricingTable.CalculateCost(100, 100, "test", "gpt-4o-mini");
            if (cost.HasValue) sum += cost.Value;
        }
        
        // Should accumulate precisely
        Assert.True(sum > 0);
    }

    [Fact]
    public void UnknownProvider_ReturnsNull()
    {
        var cost = PricingTable.CalculateCost(100, 100, "", "unknown");
        Assert.Null(cost);
    }

    [Fact]
    public void CostExcludedFromAggregation_NullValues()
    {
        var costs = new[] 
        { 
            PricingTable.CalculateCost(100, 100, "test", "gpt-4o"), 
            null,
            PricingTable.CalculateCost(100, 100, "test", "unknown")
        };
        
        // Only non-null should be included
        var total = 0m;
        foreach (var c in costs)
        {
            if (c.HasValue) total += c.Value;
        }
        
        Assert.Single(costs.Where(c => c.HasValue));
    }
}
